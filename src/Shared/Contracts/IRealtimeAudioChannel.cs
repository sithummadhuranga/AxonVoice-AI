namespace AxonVoiceAI.Shared.Contracts;

public enum ChannelType { WebSocket, WhatsApp, Phone }

public enum InboundAudioFrameKind { Audio, AudioStreamEnd }

public enum OutboundAudioControlKind { InterruptPlayback }

public sealed record InboundAudioFrame
{
    public InboundAudioFrameKind Kind { get; init; }

    public byte[] AudioChunk { get; init; } = [];

    public static InboundAudioFrame Audio(byte[] audioChunk)
    {
        return new InboundAudioFrame
        {
            Kind = InboundAudioFrameKind.Audio,
            AudioChunk = audioChunk,
        };
    }

    public static InboundAudioFrame AudioStreamEnd()
    {
        return new InboundAudioFrame
        {
            Kind = InboundAudioFrameKind.AudioStreamEnd,
        };
    }
}

public sealed record OutboundAudioControl
{
    public OutboundAudioControlKind Kind { get; init; }

    public static OutboundAudioControl InterruptPlayback()
    {
        return new OutboundAudioControl
        {
            Kind = OutboundAudioControlKind.InterruptPlayback,
        };
    }
}

public interface IRealtimeAudioChannel
{
    ChannelType Type { get; }
    IAsyncEnumerable<InboundAudioFrame> GetInboundAudioFramesAsync(CancellationToken ct);
    Task SendOutboundAudioAsync(byte[] audioChunk, CancellationToken ct);
    Task SendOutboundControlAsync(OutboundAudioControl control, CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
}
