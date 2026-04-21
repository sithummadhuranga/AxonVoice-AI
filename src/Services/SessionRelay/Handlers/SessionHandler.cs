using AxonVoiceAI.SessionRelay.Audio;
using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.SessionRelay.Prompts;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Net.Http.Json;
using System.Text.Json;

namespace AxonVoiceAI.SessionRelay.Handlers;

/// <summary>
/// Orchestrates a single live session: fetches agent config and knowledge,
/// connects to Gemini, and runs the bidirectional audio forwarding loop.
/// </summary>
public sealed class SessionHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SessionHandler> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly KernelPluginCollection _plugins;

    public SessionHandler(
        IHttpClientFactory httpClientFactory,
        KernelPluginCollection plugins,
        ILogger<SessionHandler> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _plugins = plugins;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task RunSessionAsync(
        SessionStartContextDto context,
        IRealtimeAudioChannel channel,
        CancellationToken ct)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = context.TenantId,
            ["AgentId"] = context.AgentId,
            ["SessionId"] = context.SessionId,
        });

        _logger.LogInformation("Session starting.");

        // Fetch agent config and knowledge base context in parallel.
        var agentConfigTask = FetchAgentConfigAsync(context.AgentId, context.TenantId, ct);
        var knowledgeTask = FetchKnowledgeAsync(context.AgentId, context.TenantId, ct);

        await Task.WhenAll(agentConfigTask, knowledgeTask);

        var agentConfig = await agentConfigTask;
        var knowledgeChunks = await knowledgeTask;

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, knowledgeChunks);

        var geminiClient = new GeminiLiveClient(
            _loggerFactory.CreateLogger<GeminiLiveClient>(),
            context.SessionId.ToString());

        await using (geminiClient)
        {
            await geminiClient.ConnectAsync(agentConfig.GeminiApiKey, sessionConfig, ct);

            var interceptor = new FunctionCallInterceptor(
                BuildKernel(context),
                channel,
                agentConfig.Language,
                context.TenantId.ToString(),
                context.AgentId.ToString(),
                context.SessionId.ToString(),
                _loggerFactory.CreateLogger<FunctionCallInterceptor>());

            // Run inbound (caller → Gemini) and outbound (Gemini → caller) loops concurrently.
            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var inboundTask = ForwardInboundAudioAsync(channel, geminiClient, loopCts.Token);
            var outboundTask = ForwardOutboundAudioAsync(geminiClient, channel, interceptor, loopCts.Token);

            try
            {
                await Task.WhenAny(inboundTask, outboundTask);
            }
            finally
            {
                await loopCts.CancelAsync();
                await Task.WhenAll(inboundTask, outboundTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }

        _logger.LogInformation("Session ended.");
    }

    private static async Task ForwardInboundAudioAsync(
        IRealtimeAudioChannel channel,
        GeminiLiveClient geminiClient,
        CancellationToken ct)
    {
        await foreach (var chunk in channel.GetInboundAudioStreamAsync(ct))
        {
            await geminiClient.SendAudioChunkAsync(chunk, ct);
        }
    }

    private static async Task ForwardOutboundAudioAsync(
        GeminiLiveClient geminiClient,
        IRealtimeAudioChannel channel,
        FunctionCallInterceptor interceptor,
        CancellationToken ct)
    {
        await foreach (var message in geminiClient.ReceiveMessagesAsync(ct))
        {
            if (message.ToolCall is { FunctionCalls.Count: > 0 })
            {
                var responses = await interceptor.HandleAsync(message.ToolCall.FunctionCalls, geminiClient, ct);
                // Tool responses are queued back to Gemini via the relay's response channel (future wiring).
                continue;
            }

            if (message.ServerContent?.ModelTurn is { Parts.Count: > 0 })
            {
                foreach (var part in message.ServerContent.ModelTurn.Parts)
                {
                    if (part.InlineData is { MimeType: var mime } inlineData && mime.StartsWith("audio/"))
                    {
                        var audioBytes = Convert.FromBase64String(inlineData.Data);
                        await channel.SendOutboundAudioAsync(audioBytes, ct);
                    }
                }
            }
        }
    }

    private async Task<AgentConfigDto> FetchAgentConfigAsync(Guid agentId, Guid tenantId, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("agent-config");
        var response = await client.GetAsync($"/agents/{agentId}/config", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentConfigDto>(ct)
            ?? throw new InvalidOperationException($"Agent config not found for agent {agentId}.");
    }

    private async Task<IReadOnlyList<KnowledgeChunkDto>> FetchKnowledgeAsync(Guid agentId, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("knowledge-base");
            var response = await client.GetAsync($"/agents/{agentId}/knowledge/context", ct);
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<KnowledgeChunkDto>>(ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge base fetch failed — proceeding without knowledge context.");
            return [];
        }
    }

    private Kernel BuildKernel(SessionStartContextDto context)
    {
        var builder = Kernel.CreateBuilder();
        foreach (var plugin in _plugins)
            builder.Plugins.Add(plugin);
        return builder.Build();
    }
}
