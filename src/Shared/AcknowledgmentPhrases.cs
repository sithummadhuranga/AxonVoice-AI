namespace AxonVoiceAI.Shared;

/// <summary>
/// Per-language acknowledgment phrases injected into Gemini as a text instruction
/// when a function call is being processed. Selected based on the detected session language.
/// </summary>
public static class AcknowledgmentPhrases
{
    private static readonly IReadOnlyDictionary<string, string> Phrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["si"] = "හොඳයි, මමත් පරීක්ෂා කරලා බලන්නම්",
            ["ta"] = "சரி, நான் சரிபார்க்கிறேன்",
            ["en"] = "Let me check that for you",
        };

    /// <summary>
    /// Returns the acknowledgment phrase for the given language code.
    /// Falls back to English if the language code is not found.
    /// </summary>
    public static string ForLanguage(string languageCode) =>
        Phrases.TryGetValue(languageCode, out var phrase) ? phrase : Phrases["en"];
}
