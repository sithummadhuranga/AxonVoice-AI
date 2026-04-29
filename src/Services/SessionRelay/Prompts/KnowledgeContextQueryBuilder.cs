namespace AxonVoiceAI.SessionRelay.Prompts;

public static class KnowledgeContextQueryBuilder
{
    private const string BaseQuery =
        "products services pricing menu items availability reservations appointments booking order pickup delivery opening hours location policies contact details treatments packages tickets seating durations";

    public static string BuildDefault(string? preferredLanguage)
    {
        var languageHint = NormalizeLanguageHint(preferredLanguage);
        return string.IsNullOrEmpty(languageHint)
            ? $"{BaseQuery} sinhala tamil english"
            : $"{BaseQuery} {languageHint}";
    }

    private static string NormalizeLanguageHint(string? preferredLanguage)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage))
            return string.Empty;

        return preferredLanguage.Trim().ToLowerInvariant() switch
        {
            "si" => "sinhala",
            "ta" => "tamil",
            "en" => "english",
            _ => string.Empty,
        };
    }
}