using AxonVoiceAI.Shared.Contracts;
using System.Buffers;
using System.Net.WebSockets;

namespace AxonVoiceAI.SessionRelay.Audio;

/// <summary>
/// Implements <see cref="IRealtimeAudioChannel"/> over a browser WebSocket connection
/// carrying raw 16-bit PCM audio at 16 kHz, mono.
/// </summary>
public sealed class WebSocketAudioChannel : IRealtimeAudioChannel
{
    private readonly WebSocket _webSocket;

    public ChannelType Type => ChannelType.WebSocket;

    public WebSocketAudioChannel(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async IAsyncEnumerable<byte[]> GetInboundAudioStreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    yield break;

                if (result.MessageType != WebSocketMessageType.Binary)
                    continue;

                var chunk = new byte[result.Count];
                buffer.AsSpan(0, result.Count).CopyTo(chunk);
                yield return chunk;
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

    public async Task CloseAsync(CancellationToken ct)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", ct);
        }
    }
}
