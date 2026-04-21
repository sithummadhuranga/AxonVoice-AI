namespace AxonVoiceAI.Shared.Contracts;

public enum ChannelType { WebSocket, WhatsApp, Phone }

public interface IRealtimeAudioChannel
{
    ChannelType Type { get; }
    IAsyncEnumerable<byte[]> GetInboundAudioStreamAsync(CancellationToken ct);
    Task SendOutboundAudioAsync(byte[] audioChunk, CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
}
