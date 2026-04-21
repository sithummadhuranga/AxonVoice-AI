using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.SessionRelay.Gemini;

/// <summary>
/// Manages the outbound WebSocket connection to the Gemini Live API for a single session.
/// Responsible for session setup, audio forwarding, and receiving Gemini messages.
/// </summary>
public sealed class GeminiLiveClient : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly ILogger<GeminiLiveClient> _logger;
    private readonly string _sessionId;

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
        var setupBytes = Encoding.UTF8.GetBytes(setupJson);
        await _webSocket.SendAsync(setupBytes, WebSocketMessageType.Text, endOfMessage: true, ct);

        _logger.LogInformation("Gemini session connected. SessionId={SessionId}", _sessionId);
    }

    public async Task SendAudioChunkAsync(byte[] pcmData, CancellationToken ct)
    {
        var message = new
        {
            realtimeInput = new
            {
                mediaChunks = new[]
                {
                    new { mimeType = "audio/pcm;rate=16000", data = Convert.ToBase64String(pcmData) }
                }
            }
        };

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    public async IAsyncEnumerable<GeminiMessage> ReceiveMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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
                    _logger.LogInformation("Gemini WebSocket closed. SessionId={SessionId}", _sessionId);
                    yield break;
                }

                messageBuffer.AddRange(buffer.AsSpan(0, result.Count).ToArray());

                if (!result.EndOfMessage)
                    continue;

                var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                GeminiMessage? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<GeminiMessage>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize Gemini message. SessionId={SessionId}", _sessionId);
                }

                if (message is not null)
                    yield return message;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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
        _webSocket.Dispose();
    }
}

public sealed record GeminiMessage
{
    [JsonPropertyName("setupComplete")]
    public object? SetupComplete { get; init; }

    [JsonPropertyName("serverContent")]
    public GeminiServerContent? ServerContent { get; init; }

    [JsonPropertyName("toolCall")]
    public GeminiToolCall? ToolCall { get; init; }
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

public sealed record GeminiFunctionCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("args")]
    public JsonElement? Args { get; init; }
}
