using System.Text.Json.Serialization;

namespace AxonVoiceAI.SessionRelay.Gemini;

/// <summary>
/// Represents the session setup payload sent to Gemini Live API on WebSocket connection.
/// Configured for audio-only modality using the platform's supported voice.
/// </summary>
public sealed record GeminiSessionConfig
{
    [JsonPropertyName("setup")]
    public GeminiSetup Setup { get; init; } = null!;
}

public sealed record GeminiSetup
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "models/gemini-2.5-flash-native-audio-preview-12-2025";

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig GenerationConfig { get; init; } = new();

    [JsonPropertyName("systemInstruction")]
    public GeminiSystemInstruction SystemInstruction { get; init; } = null!;

    [JsonPropertyName("tools")]
    public IReadOnlyList<GeminiToolDeclaration> Tools { get; init; } = [];
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("responseModalities")]
    public string[] ResponseModalities { get; init; } = ["AUDIO"];

    [JsonPropertyName("speechConfig")]
    public GeminiSpeechConfig SpeechConfig { get; init; } = new();
}

public sealed record GeminiSpeechConfig
{
    [JsonPropertyName("voiceConfig")]
    public GeminiVoiceConfig VoiceConfig { get; init; } = new();
}

public sealed record GeminiVoiceConfig
{
    [JsonPropertyName("prebuiltVoiceConfig")]
    public GeminiPrebuiltVoice PrebuiltVoiceConfig { get; init; } = new();
}

public sealed record GeminiPrebuiltVoice
{
    [JsonPropertyName("voiceName")]
    public string VoiceName { get; init; } = "Aoede";
}

public sealed record GeminiSystemInstruction
{
    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiTextPart> Parts { get; init; } = [];
}

public sealed record GeminiTextPart
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record GeminiToolDeclaration
{
    [JsonPropertyName("functionDeclarations")]
    public IReadOnlyList<GeminiFunctionDeclaration> FunctionDeclarations { get; init; } = [];
}

public sealed record GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; }
}
