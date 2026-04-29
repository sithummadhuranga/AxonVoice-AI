using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;
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
        IGeminiLiveClient geminiClient,
        CancellationToken ct)
    {
        var responseTasks = functionCalls
            .Select(functionCall => HandleSingleAsync(functionCall, ct))
            .ToArray();

        try
        {
            await Task.WhenAll(responseTasks.Select(static task => (Task)task));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return [];
        }

        return responseTasks
            .Select(task => task.Result)
            .Where(static response => response is not null)
            .Cast<GeminiFunctionResponse>()
            .ToArray();
    }

    /// <summary>
    /// No-op. Acknowledgment is now handled by the model itself: the system prompt instructs the
    /// model to speak a brief phrase before calling any tool. The previous clientContent injection
    /// was interrupting model generation mid-call and corrupting conversation state.
    /// Kept for backward compatibility with existing callers.
    /// </summary>
    internal Task SendAcknowledgmentAsync(IGeminiLiveClient geminiClient, CancellationToken ct)
    {
        _logger.LogDebug(
            "Acknowledgment handled by model via system prompt. SessionId={SessionId}",
            _sessionId);
        return Task.CompletedTask;
    }

    internal async Task<GeminiFunctionResponse?> HandleSingleAsync(GeminiFunctionCall functionCall, CancellationToken ct)
    {
        var executionResult = await HandleSingleWithTelemetryAsync(functionCall, ct);
        return executionResult?.Response;
    }

    internal async Task<FunctionCallExecutionResult?> HandleSingleWithTelemetryAsync(
        GeminiFunctionCall functionCall,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var argumentsJson = functionCall.Args?.GetRawText();

        try
        {
            var response = await InvokeSinglePluginAsync(functionCall, ct);
            if (response is null)
                return null;

            stopwatch.Stop();

            return new FunctionCallExecutionResult(
                functionCall.Name,
                argumentsJson,
                response.ResponseJson,
                !ContainsErrorPayload(response.ResponseJson),
                ExtractErrorMessage(response.ResponseJson),
                stopwatch.ElapsedMilliseconds is > int.MaxValue ? int.MaxValue : (int)stopwatch.ElapsedMilliseconds,
                response);
        }
        catch
        {
            stopwatch.Stop();
            throw;
        }
    }

    private async Task<GeminiFunctionResponse?> InvokeSinglePluginAsync(
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Plugin function {FunctionName} was cancelled before completion. CallId={CallId}",
                call.Name,
                call.Id);

            return null;
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
        arguments["tenantId"] = _tenantId;

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

    private static bool ContainsErrorPayload(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractErrorMessage(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!document.RootElement.TryGetProperty("error", out var errorElement))
                return null;

            return errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()
                : errorElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

}

public record GeminiFunctionResponse(string CallId, string FunctionName, string ResponseJson);

public sealed record FunctionCallExecutionResult(
    string FunctionName,
    string? ArgumentsJson,
    string ResultJson,
    bool Succeeded,
    string? ErrorMessage,
    int DurationMs,
    GeminiFunctionResponse Response);
