namespace AxonVoiceAI.SessionRelay.Prompts;

public static class KnowledgeContextQueryBuilder
{
    private const string BaseQuery =
        "menu items food drinks prices reservations booking availability opening hours location services policies contact details";

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