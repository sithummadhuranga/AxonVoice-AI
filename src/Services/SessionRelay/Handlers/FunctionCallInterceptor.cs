using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.Shared;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace AxonVoiceAI.SessionRelay.Handlers;

/// <summary>
/// Intercepts Gemini tool_call messages, dispatches the function through Semantic Kernel,
/// and injects a spoken acknowledgment phrase to keep the audio stream alive during processing.
/// The acknowledgment injection and plugin dispatch happen in parallel so the caller
/// receives an immediate audio response while the real work runs.
/// </summary>
public sealed class FunctionCallInterceptor
{
    private readonly Kernel _kernel;
    private readonly ILogger<FunctionCallInterceptor> _logger;
    private readonly string _language;
    private readonly string _tenantId;
    private readonly string _agentId;
    private readonly string _sessionId;

    public FunctionCallInterceptor(
        Kernel kernel,
        string language,
        string tenantId,
        string agentId,
        string sessionId,
        ILogger<FunctionCallInterceptor> logger)
    {
        _kernel = kernel;
        _language = language;
        _tenantId = tenantId;
        _agentId = agentId;
        _sessionId = sessionId;
        _logger = logger;
    }

    /// <summary>
    /// Handles a batch of function calls from Gemini.
    /// Returns the function response payloads to send back to Gemini.
    /// </summary>
    public async Task<IReadOnlyList<GeminiFunctionResponse>> HandleAsync(
        IReadOnlyList<GeminiFunctionCall> functionCalls,
        GeminiLiveClient geminiClient,
        CancellationToken ct)
    {
        // Fire the acknowledgment audio in parallel with plugin dispatch.
        var acknowledgmentTask = SendAcknowledgmentAsync(geminiClient, ct);
        var pluginDispatchTask = DispatchPluginsAsync(functionCalls, ct);

        await Task.WhenAll(acknowledgmentTask, pluginDispatchTask);
        return await pluginDispatchTask;
    }

    private async Task SendAcknowledgmentAsync(GeminiLiveClient geminiClient, CancellationToken ct)
    {
        var phrase = AcknowledgmentPhrases.ForLanguage(_language);
        // Send the acknowledgment as a text turn so Gemini speaks it immediately.
        // The exact mechanism depends on the Gemini Live API's client-content endpoint.
        // This is a placeholder that will be wired when exact API response format is confirmed.
        _logger.LogDebug(
            "Acknowledgment phrase queued: '{Phrase}'. SessionId={SessionId}",
            phrase, _sessionId);

        await Task.CompletedTask;
    }

    private async Task<IReadOnlyList<GeminiFunctionResponse>> DispatchPluginsAsync(
        IReadOnlyList<GeminiFunctionCall> functionCalls,
        CancellationToken ct)
    {
        var responses = new List<GeminiFunctionResponse>(functionCalls.Count);

        foreach (var call in functionCalls)
        {
            var response = await InvokeSinglePluginAsync(call, ct);
            responses.Add(response);
        }

        return responses.AsReadOnly();
    }

    private async Task<GeminiFunctionResponse> InvokeSinglePluginAsync(
        GeminiFunctionCall call,
        CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = _tenantId,
            ["AgentId"] = _agentId,
            ["SessionId"] = _sessionId,
        });

        _logger.LogInformation("Dispatching plugin function: {FunctionName}", call.Name);

        try
        {
            var arguments = new KernelArguments();
            if (call.Args.HasValue)
            {
                foreach (var property in call.Args.Value.EnumerateObject())
                {
                    arguments[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.TryGetInt32(out var i) ? (object)i : property.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => property.Value.GetRawText(),
                    };
                }
            }

            ApplyArgumentAliases(arguments);
            InjectSessionContextArguments(call.Name, arguments);

            // Search all loaded plugins for a function matching the name Gemini specified.
            KernelFunction? function = null;
            foreach (var plugin in _kernel.Plugins)
            {
                if (plugin.TryGetFunction(call.Name, out var found))
                {
                    function = found;
                    break;
                }
            }
            if (function is null)
                throw new InvalidOperationException($"No plugin function named '{call.Name}' is registered.");

            var functionResult = await _kernel.InvokeAsync(function, arguments, ct);
            var resultJson = JsonSerializer.Serialize(functionResult.GetValue<object>());

            return new GeminiFunctionResponse(call.Id, call.Name, resultJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin function {FunctionName} threw an exception.", call.Name);
            var errorPayload = JsonSerializer.Serialize(new { error = ex.Message });
            return new GeminiFunctionResponse(call.Id, call.Name, errorPayload);
        }
    }

    private void InjectSessionContextArguments(string functionName, KernelArguments arguments)
    {
        arguments["agentId"] = _agentId;

        if (string.Equals(functionName, "create_pending_booking", StringComparison.Ordinal))
        {
            arguments["sessionId"] = _sessionId;
            arguments["detectedLanguage"] = _language;
        }
    }

    private static void ApplyArgumentAliases(KernelArguments arguments)
    {
        CopyArgument(arguments, "party_size", "partySize");
        CopyArgument(arguments, "customer_name", "customerName");
        CopyArgument(arguments, "contact_number", "customerPhone");
        CopyArgument(arguments, "notes", "specialRequests");
    }

    private static void CopyArgument(KernelArguments arguments, string sourceName, string targetName)
    {
        if (arguments.ContainsName(targetName))
            return;

        if (arguments.TryGetValue(sourceName, out var value))
            arguments[targetName] = value;
    }
}

public record GeminiFunctionResponse(string CallId, string FunctionName, string ResponseJson);
