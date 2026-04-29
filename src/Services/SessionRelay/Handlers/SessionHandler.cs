using AxonVoiceAI.SessionRelay.Audio;
using AxonVoiceAI.SessionRelay.ConversationStore;
using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.SessionRelay.Prompts;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;

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
    private readonly ConversationStoreSessionWriter _sessionWriter;

    public SessionHandler(
        IHttpClientFactory httpClientFactory,
        KernelPluginCollection plugins,
        ConversationStoreSessionWriter sessionWriter,
        ILogger<SessionHandler> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _plugins = plugins;
        _sessionWriter = sessionWriter;
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
        var knowledgeTask = FetchKnowledgeAsync(context.AgentId, context.TenantId, context.Language, ct);

        await Task.WhenAll(agentConfigTask, knowledgeTask);

        var agentConfig = await agentConfigTask;
        var knowledgeChunks = await knowledgeTask;
        var sessionLanguage = string.IsNullOrWhiteSpace(context.Language)
            ? agentConfig.Language
            : context.Language;

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, knowledgeChunks);
        var sessionStopwatch = Stopwatch.StartNew();

        var geminiClient = new GeminiLiveClient(
            _loggerFactory.CreateLogger<GeminiLiveClient>(),
            context.SessionId.ToString());

        await using (geminiClient)
        {
            await geminiClient.ConnectAsync(agentConfig.GeminiApiKey, sessionConfig, ct);
            await _sessionWriter.RecordSessionStartAsync(context, channel.Type, sessionLanguage, ct);

            var interceptor = new FunctionCallInterceptor(
                BuildKernel(),
                sessionLanguage,
                context.TenantId.ToString(),
                context.AgentId.ToString(),
                context.SessionId.ToString(),
                _loggerFactory.CreateLogger<FunctionCallInterceptor>());

            // Run inbound (caller → Gemini) and outbound (Gemini → caller) loops concurrently.
            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var inboundTask = ForwardInboundAudioAsync(channel, geminiClient, loopCts.Token);
            var outboundTask = ForwardOutboundAudioAsync(context, geminiClient, channel, interceptor, loopCts.Token);

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

        sessionStopwatch.Stop();
        await _sessionWriter.CloseSessionAsync(
            context,
            sessionStopwatch.Elapsed.TotalSeconds >= int.MaxValue ? int.MaxValue : (int)sessionStopwatch.Elapsed.TotalSeconds,
            ct);

        _logger.LogInformation("Session ended.");
    }

    private static async Task ForwardInboundAudioAsync(
        IRealtimeAudioChannel channel,
        GeminiLiveClient geminiClient,
        CancellationToken ct)
    {
        await foreach (var frame in channel.GetInboundAudioFramesAsync(ct))
        {
            switch (frame.Kind)
            {
                case InboundAudioFrameKind.Audio:
                    await geminiClient.SendAudioChunkAsync(frame.AudioChunk, ct);
                    break;
                case InboundAudioFrameKind.AudioStreamEnd:
                    await geminiClient.SendAudioStreamEndAsync(ct);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported inbound audio frame kind '{frame.Kind}'.");
            }
        }
    }

    private async Task ForwardOutboundAudioAsync(
        SessionStartContextDto context,
        GeminiLiveClient geminiClient,
        IRealtimeAudioChannel channel,
        FunctionCallInterceptor interceptor,
        CancellationToken ct)
    {
        var pendingToolTasks = new List<Task>();
        var pendingToolCalls = new ConcurrentDictionary<string, PendingToolCall>(StringComparer.Ordinal);

        try
        {
            await foreach (var message in geminiClient.ReceiveMessagesAsync(ct))
            {
                pendingToolTasks.RemoveAll(task => task.IsCompleted);

                if (message.ToolCall is { FunctionCalls.Count: > 0 })
                {
                    pendingToolTasks.Add(HandleToolCallsAsync(context, geminiClient, interceptor, message.ToolCall.FunctionCalls, pendingToolCalls, ct));
                    continue;
                }

                if (message.ToolCallCancellation is { Ids.Count: > 0 })
                {
                    CancelPendingToolCalls(message.ToolCallCancellation.Ids, pendingToolCalls);
                    continue;
                }

                if (message.ServerContent?.Interrupted is true)
                {
                    await channel.SendOutboundControlAsync(OutboundAudioControl.InterruptPlayback(), ct);
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
        finally
        {
            if (pendingToolTasks.Count > 0)
            {
                await Task.WhenAll(pendingToolTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            foreach (var pendingToolCall in pendingToolCalls.Values)
            {
                pendingToolCall.Dispose();
            }
        }
    }

    private async Task HandleToolCallsAsync(
        SessionStartContextDto context,
        GeminiLiveClient geminiClient,
        FunctionCallInterceptor interceptor,
        IReadOnlyList<GeminiFunctionCall> functionCalls,
        ConcurrentDictionary<string, PendingToolCall> pendingToolCalls,
        CancellationToken ct)
    {
        var batchCalls = RegisterPendingToolCalls(functionCalls, pendingToolCalls, ct);
        if (batchCalls.Count == 0)
            return;

        try
        {
            // The model speaks an acknowledgment phrase naturally before emitting the toolCall message
            // (instructed via the system prompt). No clientContent injection is needed here — sending
            // clientContent mid-session interrupts model generation and corrupts conversation state.
            var executionTasks = batchCalls
                .Select(pendingToolCall => interceptor.HandleSingleWithTelemetryAsync(pendingToolCall.FunctionCall, pendingToolCall.CancellationTokenSource.Token))
                .ToArray();

            await Task.WhenAll(executionTasks.Select(static task => (Task)task));

            var completedExecutions = batchCalls
                .Select((pendingToolCall, index) => new
                {
                    PendingToolCall = pendingToolCall,
                    Execution = executionTasks[index].Result,
                })
                .Where(static item => !item.PendingToolCall.CancellationTokenSource.IsCancellationRequested && item.Execution is not null)
                .Select(static item => item.Execution!)
                .ToArray();

            var responses = completedExecutions
                .Select(static execution => execution.Response)
                .ToArray();

            if (responses.Length > 0)
            {
                await Task.WhenAll(
                    geminiClient.SendToolResponsesAsync(responses, ct),
                    _sessionWriter.RecordFunctionCallsAsync(context, completedExecutions, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool call handling failed before responses could be delivered to Gemini.");
        }
        finally
        {
            foreach (var batchCall in batchCalls)
            {
                pendingToolCalls.TryRemove(batchCall.FunctionCall.Id, out _);
                batchCall.Dispose();
            }
        }
    }

    private List<PendingToolCall> RegisterPendingToolCalls(
        IReadOnlyList<GeminiFunctionCall> functionCalls,
        ConcurrentDictionary<string, PendingToolCall> pendingToolCalls,
        CancellationToken ct)
    {
        var batchCalls = new List<PendingToolCall>(functionCalls.Count);

        foreach (var functionCall in functionCalls)
        {
            var pendingToolCall = new PendingToolCall(functionCall, CancellationTokenSource.CreateLinkedTokenSource(ct));

            if (!pendingToolCalls.TryAdd(functionCall.Id, pendingToolCall))
            {
                pendingToolCall.Dispose();
                _logger.LogWarning("Received duplicate Gemini tool call id {ToolCallId}; ignoring duplicate.", functionCall.Id);
                continue;
            }

            batchCalls.Add(pendingToolCall);
        }

        return batchCalls;
    }

    private void CancelPendingToolCalls(
        IReadOnlyList<string> ids,
        ConcurrentDictionary<string, PendingToolCall> pendingToolCalls)
    {
        foreach (var id in ids)
        {
            if (pendingToolCalls.TryGetValue(id, out var pendingToolCall))
            {
                if (pendingToolCall.TryCancel())
                {
                    _logger.LogInformation("Cancelled Gemini tool call {ToolCallId}.", id);
                }

                continue;
            }

            _logger.LogDebug("Received Gemini tool call cancellation for unknown or completed call {ToolCallId}.", id);
        }
    }

    private async Task<AgentConfigDto> FetchAgentConfigAsync(Guid agentId, Guid tenantId, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("agent-config");
        var response = await client.GetAsync($"/agents/{agentId}/config?tenantId={tenantId:D}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentConfigDto>(ct)
            ?? throw new InvalidOperationException($"Agent config not found for agent {agentId}.");
    }

    private async Task<IReadOnlyList<KnowledgeChunkDto>> FetchKnowledgeAsync(
        Guid agentId,
        Guid tenantId,
        string sessionLanguage,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("knowledge-base");
            var retrievalQuery = KnowledgeContextQueryBuilder.BuildDefault(sessionLanguage);
            var requestUri = $"/internal/agents/{agentId}/knowledge/context?tenantId={tenantId:D}&query={Uri.EscapeDataString(retrievalQuery)}";
            var response = await client.GetAsync(requestUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Knowledge base context request returned {StatusCode} for agent {AgentId} and tenant {TenantId}.",
                    (int)response.StatusCode,
                    agentId,
                    tenantId);
                return [];
            }

            return await response.Content.ReadFromJsonAsync<IReadOnlyList<KnowledgeChunkDto>>(ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge base fetch failed — proceeding without knowledge context.");
            return [];
        }
    }

    private Kernel BuildKernel()
    {
        var builder = Kernel.CreateBuilder();
        foreach (var plugin in _plugins)
            builder.Plugins.Add(plugin);
        return builder.Build();
    }

    private sealed class PendingToolCall(GeminiFunctionCall functionCall, CancellationTokenSource cancellationTokenSource) : IDisposable
    {
        private int _disposed;

        public GeminiFunctionCall FunctionCall { get; } = functionCall;

        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public bool TryCancel()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return false;

            try
            {
                CancellationTokenSource.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                CancellationTokenSource.Dispose();
            }
        }
    }
}
