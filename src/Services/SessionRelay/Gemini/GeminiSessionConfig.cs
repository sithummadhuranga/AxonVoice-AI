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
    public string Model { get; init; } = "models/gemini-2.0-flash-live";

    [JsonPropertyName("generation_config")]
    public GeminiGenerationConfig GenerationConfig { get; init; } = new();

    [JsonPropertyName("system_instruction")]
    public GeminiSystemInstruction SystemInstruction { get; init; } = null!;

    [JsonPropertyName("tools")]
    public IReadOnlyList<GeminiToolDeclaration> Tools { get; init; } = [];
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("response_modalities")]
    public string[] ResponseModalities { get; init; } = ["AUDIO"];

    [JsonPropertyName("speech_config")]
    public GeminiSpeechConfig SpeechConfig { get; init; } = new();
}

public sealed record GeminiSpeechConfig
{
    [JsonPropertyName("voice_config")]
    public GeminiVoiceConfig VoiceConfig { get; init; } = new();
}

public sealed record GeminiVoiceConfig
{
    [JsonPropertyName("prebuilt_voice_config")]
    public GeminiPrebuiltVoice PrebuiltVoiceConfig { get; init; } = new();
}

public sealed record GeminiPrebuiltVoice
{
    [JsonPropertyName("voice_name")]
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
    [JsonPropertyName("function_declarations")]
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
