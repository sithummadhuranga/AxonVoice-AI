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

    [JsonPropertyName("realtimeInputConfig")]
    public GeminiRealtimeInputConfig RealtimeInputConfig { get; init; } = new();
}

/// <summary>
/// Controls how Gemini handles voice activity detection and turn completion for live audio input.
/// Without explicit configuration the model defaults are too conservative, causing long pauses
/// before it detects that the caller has finished speaking.
/// </summary>
public sealed record GeminiRealtimeInputConfig
{
    [JsonPropertyName("automaticActivityDetection")]
    public GeminiAutomaticActivityDetection AutomaticActivityDetection { get; init; } = new();

    /// <summary>
    /// START_OF_ACTIVITY_INTERRUPTS: detecting new speech from the caller interrupts the model
    /// mid-response, which is the natural behaviour for a voice conversation.
    /// </summary>
    [JsonPropertyName("activityHandling")]
    public string ActivityHandling { get; init; } = "START_OF_ACTIVITY_INTERRUPTS";

    /// <summary>
    /// TURN_INCLUDES_ONLY_ACTIVITY: only audio bytes flagged as speech are forwarded to the model
    /// context, eliminating background noise from the turn window and reducing latency.
    /// </summary>
    [JsonPropertyName("turnCoverage")]
    public string TurnCoverage { get; init; } = "TURN_INCLUDES_ONLY_ACTIVITY";
}

public sealed record GeminiAutomaticActivityDetection
{
    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; } = false;

    /// <summary>High sensitivity so the model reacts quickly when the caller starts speaking.</summary>
    [JsonPropertyName("startOfSpeechSensitivity")]
    public string StartOfSpeechSensitivity { get; init; } = "START_SENSITIVITY_HIGH";

    /// <summary>High sensitivity so the model stops listening promptly once the caller is silent.</summary>
    [JsonPropertyName("endOfSpeechSensitivity")]
    public string EndOfSpeechSensitivity { get; init; } = "END_SENSITIVITY_HIGH";

    /// <summary>20 ms of audio prepended before detected speech start to avoid clipping the first phoneme.</summary>
    [JsonPropertyName("prefixPaddingMs")]
    public int PrefixPaddingMs { get; init; } = 20;

    /// <summary>
    /// 200 ms of trailing silence triggers server-side turn completion. Combined with the widget's
    /// client-side audioStreamEnd signal (sent after 4 consecutive silent 64 ms chunks = 256 ms),
    /// the effective end-of-turn detection is approximately 200–256 ms of silence, which is tight
    /// enough for natural phone-style turn-taking without clipping word endings.
    /// </summary>
    [JsonPropertyName("silenceDurationMs")]
    public int SilenceDurationMs { get; init; } = 200;
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("responseModalities")]
    public string[] ResponseModalities { get; init; } = ["AUDIO"];

    [JsonPropertyName("speechConfig")]
    public GeminiSpeechConfig SpeechConfig { get; init; } = new();

    [JsonPropertyName("thinkingConfig")]
    public GeminiThinkingConfig ThinkingConfig { get; init; } = new();
}

/// <summary>
/// Gemini 2.5 Flash Live enables dynamic thinking by default. For fast phone-style turns
/// we disable it so the model starts speaking with lower latency on straightforward tasks.
/// </summary>
public sealed record GeminiThinkingConfig
{
    [JsonPropertyName("thinkingBudget")]
    public int ThinkingBudget { get; init; } = 0;
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
    public string VoiceName { get; init; } = "Sulafat";
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
