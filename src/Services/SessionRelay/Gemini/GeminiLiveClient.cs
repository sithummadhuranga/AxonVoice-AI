using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AxonVoiceAI.SessionRelay.Handlers;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.SessionRelay.Gemini;

public interface IGeminiLiveClient
{
    Task SendClientContentTextTurnAsync(string text, CancellationToken ct);
}

/// <summary>
/// Manages the outbound WebSocket connection to the Gemini Live API for a single session.
/// Responsible for session setup, audio forwarding, and receiving Gemini messages.
/// </summary>
public sealed class GeminiLiveClient : IAsyncDisposable, IGeminiLiveClient
{
    private readonly ClientWebSocket _webSocket;
    private readonly ILogger<GeminiLiveClient> _logger;
    private readonly string _sessionId;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GeminiLiveClient(ILogger<GeminiLiveClient> logger, string sessionId)
    {
        _webSocket = new ClientWebSocket();
        _logger = logger;
        _sessionId = sessionId;
    }

    public async Task ConnectAsync(string apiKey, GeminiSessionConfig config, CancellationToken ct)
    {
        var endpoint = new Uri($"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={apiKey}");
        await _webSocket.ConnectAsync(endpoint, ct);

        var setupJson = JsonSerializer.Serialize(config, JsonOptions);
        _logger.LogInformation("Sending Gemini setup message. SessionId={SessionId} Payload={Payload}", _sessionId, setupJson);
        await SendJsonMessageAsync(config, ct);

        await WaitForSetupCompletionAsync(ct);

        _logger.LogInformation("Gemini session connected. SessionId={SessionId}", _sessionId);
    }

    public async Task SendAudioChunkAsync(byte[] pcmData, CancellationToken ct)
    {
        var message = GeminiLiveRequestFactory.CreateAudioInput(pcmData);
        await SendJsonMessageAsync(message, ct);
    }

    public async Task SendToolResponsesAsync(IReadOnlyList<GeminiFunctionResponse> responses, CancellationToken ct)
    {
        if (responses.Count == 0)
            return;

        var message = GeminiLiveRequestFactory.CreateToolResponse(responses);
        await SendJsonMessageAsync(message, ct);

        _logger.LogDebug(
            "Sent {Count} tool response(s) to Gemini. SessionId={SessionId}",
            responses.Count,
            _sessionId);
    }

    public async Task SendClientContentTextTurnAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Client content text must not be empty.", nameof(text));

        var message = GeminiLiveRequestFactory.CreateClientContentTextTurn(text);
        await SendJsonMessageAsync(message, ct);

        _logger.LogDebug("Sent client-content text turn to Gemini. SessionId={SessionId}", _sessionId);
    }

    public async IAsyncEnumerable<GeminiMessage> ReceiveMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            var json = await ReceiveNextJsonMessageAsync(ct);
            if (json is null)
                yield break;

            GeminiMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<GeminiMessage>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Gemini message. SessionId={SessionId} Payload={Payload}", _sessionId, TruncateForLog(json));
            }

            if (message is null)
                continue;

            if (message.Error is not null)
            {
                _logger.LogWarning(
                    "Gemini returned an error. SessionId={SessionId} Code={Code} Status={Status} Message={Message}",
                    _sessionId,
                    message.Error.Code,
                    message.Error.Status,
                    message.Error.Message);
            }

            yield return message;
        }
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);
        _sendLock.Dispose();
        _webSocket.Dispose();
    }

    private async Task SendJsonMessageAsync<TMessage>(TMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task WaitForSetupCompletionAsync(CancellationToken ct)
    {
        using var setupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        setupCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            while (true)
            {
                var json = await ReceiveNextJsonMessageAsync(setupCts.Token);
                if (json is null)
                    throw new InvalidOperationException("Gemini closed the connection before session setup completed.");

                GeminiMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<GeminiMessage>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize Gemini setup response. SessionId={SessionId} Payload={Payload}", _sessionId, TruncateForLog(json));
                    throw new InvalidOperationException("Gemini returned an invalid setup response.", ex);
                }

                if (message is null)
                {
                    _logger.LogWarning("Gemini returned an empty setup response. SessionId={SessionId} Payload={Payload}", _sessionId, TruncateForLog(json));
                    continue;
                }

                if (message.Error is not null)
                {
                    _logger.LogWarning(
                        "Gemini rejected session setup. SessionId={SessionId} Code={Code} Status={Status} Message={Message}",
                        _sessionId,
                        message.Error.Code,
                        message.Error.Status,
                        message.Error.Message);

                    throw new InvalidOperationException($"Gemini rejected session setup: {message.Error.Message}");
                }

                if (message.SetupComplete is not null)
                    return;

                _logger.LogWarning(
                    "Received an unexpected Gemini setup response before setup completed. SessionId={SessionId} Payload={Payload}",
                    _sessionId,
                    TruncateForLog(json));
            }
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for Gemini session setup to complete.", ex);
        }
    }

    private async Task<string?> ReceiveNextJsonMessageAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        var messageBuffer = new List<byte>();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation(
                        "Gemini WebSocket closed. SessionId={SessionId} CloseStatus={CloseStatus} CloseDescription={CloseDescription}",
                        _sessionId,
                        result.CloseStatus,
                        result.CloseStatusDescription);
                    return null;
                }

                messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());

                if (!result.EndOfMessage)
                    continue;

                return Encoding.UTF8.GetString(messageBuffer.ToArray());
            }

            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string TruncateForLog(string payload)
    {
        const int maxLength = 512;
        return payload.Length <= maxLength ? payload : payload[..maxLength] + "...";
    }
}

public sealed record GeminiMessage
{
    [JsonPropertyName("setupComplete")]
    public object? SetupComplete { get; init; }

    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }

    [JsonPropertyName("serverContent")]
    public GeminiServerContent? ServerContent { get; init; }

    [JsonPropertyName("toolCall")]
    public GeminiToolCall? ToolCall { get; init; }

    [JsonPropertyName("toolCallCancellation")]
    public GeminiToolCallCancellation? ToolCallCancellation { get; init; }
}

public sealed record GeminiServerContent
{
    [JsonPropertyName("modelTurn")]
    public GeminiModelTurn? ModelTurn { get; init; }

    [JsonPropertyName("turnComplete")]
    public bool? TurnComplete { get; init; }
}

public sealed record GeminiModelTurn
{
    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiResponsePart> Parts { get; init; } = [];
}

public sealed record GeminiResponsePart
{
    [JsonPropertyName("inlineData")]
    public GeminiInlineData? InlineData { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed record GeminiInlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;
}

public sealed record GeminiToolCall
{
    [JsonPropertyName("functionCalls")]
    public IReadOnlyList<GeminiFunctionCall> FunctionCalls { get; init; } = [];
}

public sealed record GeminiToolCallCancellation
{
    [JsonPropertyName("ids")]
    public IReadOnlyList<string> Ids { get; init; } = [];
}

public sealed record GeminiError
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

public sealed record GeminiFunctionCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("args")]
    public JsonElement? Args { get; init; }
}
