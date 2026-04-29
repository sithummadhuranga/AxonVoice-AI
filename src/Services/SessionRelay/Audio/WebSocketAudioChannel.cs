using AxonVoiceAI.Shared.Contracts;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxonVoiceAI.SessionRelay.Audio;

/// <summary>
/// Implements <see cref="IRealtimeAudioChannel"/> over a browser WebSocket connection
/// carrying raw 16-bit PCM audio at 16 kHz, mono.
/// </summary>
public sealed class WebSocketAudioChannel : IRealtimeAudioChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebSocket _webSocket;

    public ChannelType Type => ChannelType.WebSocket;

    public WebSocketAudioChannel(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async IAsyncEnumerable<InboundAudioFrame> GetInboundAudioFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // 64 KB covers a single PCM chunk from the widget (4 096 samples × 2 bytes = 8 192 bytes)
        // with room to spare. Previously 4 096 bytes caused every widget chunk to be received
        // as two separate ReceiveAsync calls without EndOfMessage accumulation, yielding
        // half-frames to the Gemini input path.
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        using var messageBuffer = new System.IO.MemoryStream(8192);

        try
        {
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    yield break;

                messageBuffer.Write(buffer.AsSpan(0, result.Count));

                if (!result.EndOfMessage)
                    continue;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    yield return InboundAudioFrame.Audio(messageBuffer.ToArray());
                    messageBuffer.SetLength(0);
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var controlMessage = ParseInboundControlMessage(messageBuffer);
                    messageBuffer.SetLength(0);

                    if (controlMessage?.Type == "audio_stream_end")
                    {
                        yield return InboundAudioFrame.AudioStreamEnd();
                    }

                    continue;
                }

                messageBuffer.SetLength(0);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task SendOutboundAudioAsync(byte[] audioChunk, CancellationToken ct)
    {
        if (_webSocket.State != WebSocketState.Open)
            return;

        await _webSocket.SendAsync(audioChunk, WebSocketMessageType.Binary, endOfMessage: true, ct);
    }

    public async Task SendOutboundControlAsync(OutboundAudioControl control, CancellationToken ct)
    {
        if (_webSocket.State != WebSocketState.Open)
            return;

        var payload = control.Kind switch
        {
            OutboundAudioControlKind.InterruptPlayback => new WidgetOutboundControlMessage { Type = "interrupt_playback" },
            _ => throw new InvalidOperationException($"Unsupported outbound audio control kind '{control.Kind}'.")
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", ct);
        }
    }

    private static WidgetInboundControlMessage? ParseInboundControlMessage(System.IO.MemoryStream messageBuffer)
    {
        var json = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);

        try
        {
            return JsonSerializer.Deserialize<WidgetInboundControlMessage>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record WidgetInboundControlMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }

    private sealed record WidgetOutboundControlMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }
}
