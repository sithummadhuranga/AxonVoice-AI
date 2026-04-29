using System.Net.Http.Json;
using AxonVoiceAI.SessionRelay.Handlers;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.SessionRelay.ConversationStore;

public sealed class ConversationStoreSessionWriter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConversationStoreSessionWriter> _logger;

    public ConversationStoreSessionWriter(
        IHttpClientFactory httpClientFactory,
        ILogger<ConversationStoreSessionWriter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RecordSessionStartAsync(
        SessionStartContextDto context,
        ChannelType channelType,
        string language,
        CancellationToken ct)
    {
        var request = new RecordInternalSessionStartRequest(
            context.SessionId,
            context.AgentId,
            context.TenantId,
            channelType.ToString(),
            language);

        await PostIgnoringFailureAsync(
            "/internal/sessions",
            request,
            $"record session start for {context.SessionId}",
            ct);
    }

    public async Task RecordFunctionCallsAsync(
        SessionStartContextDto context,
        IReadOnlyList<FunctionCallExecutionResult> functionCalls,
        CancellationToken ct)
    {
        if (functionCalls.Count == 0)
            return;

        foreach (var functionCall in functionCalls)
        {
            var request = new RecordInternalFunctionCallRequest(
                context.TenantId,
                functionCall.FunctionName,
                functionCall.ArgumentsJson,
                functionCall.ResultJson,
                functionCall.Succeeded,
                functionCall.ErrorMessage,
                functionCall.DurationMs);

            await PostIgnoringFailureAsync(
                $"/internal/sessions/{context.SessionId:D}/function-calls",
                request,
                $"record function call {functionCall.FunctionName} for {context.SessionId}",
                ct);
        }
    }

    public Task CloseSessionAsync(
        SessionStartContextDto context,
        int? durationSeconds,
        CancellationToken ct)
    {
        var request = new CloseInternalSessionRequest(context.TenantId, durationSeconds);

        return PostIgnoringFailureAsync(
            $"/internal/sessions/{context.SessionId:D}/close",
            request,
            $"close session {context.SessionId}",
            ct);
    }

    private async Task PostIgnoringFailureAsync<TRequest>(
        string path,
        TRequest request,
        string operation,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("conversation-store");
            using var response = await client.PostAsJsonAsync(path, request, cancellationToken: ct);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Conversation store write failed while attempting to {Operation}.", operation);
        }
    }

    private sealed record RecordInternalSessionStartRequest(
        Guid SessionId,
        Guid AgentId,
        Guid TenantId,
        string CallerIdentifier,
        string Language);

    private sealed record RecordInternalFunctionCallRequest(
        Guid TenantId,
        string FunctionName,
        string? ArgumentsJson,
        string? ResultJson,
        bool Succeeded,
        string? ErrorMessage,
        int DurationMs);

    private sealed record CloseInternalSessionRequest(
        Guid TenantId,
        int? DurationSeconds);
}