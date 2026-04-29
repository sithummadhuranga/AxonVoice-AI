namespace AxonVoiceAI.Shared;

public static class GeminiVoiceCatalog
{
    /// <summary>
    /// Default voice for agents that have not configured one explicitly.
    /// Sulafat is categorised as "warm" in the Gemini voice catalog, which suits a friendly
    /// phone-style customer service assistant speaking Sinhala, Tamil, or English.
    /// </summary>
    public const string DefaultVoiceName = "Sulafat";

    private static readonly IReadOnlyDictionary<string, string> CanonicalVoiceNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zephyr"] = "Zephyr",
            ["Puck"] = "Puck",
            ["Charon"] = "Charon",
            ["Kore"] = "Kore",
            ["Fenrir"] = "Fenrir",
            ["Leda"] = "Leda",
            ["Orus"] = "Orus",
            ["Aoede"] = "Aoede",
            ["Callirrhoe"] = "Callirrhoe",
            ["Autonoe"] = "Autonoe",
            ["Enceladus"] = "Enceladus",
            ["Iapetus"] = "Iapetus",
            ["Umbriel"] = "Umbriel",
            ["Algieba"] = "Algieba",
            ["Despina"] = "Despina",
            ["Erinome"] = "Erinome",
            ["Algenib"] = "Algenib",
            ["Rasalgethi"] = "Rasalgethi",
            ["Laomedeia"] = "Laomedeia",
            ["Achernar"] = "Achernar",
            ["Alnilam"] = "Alnilam",
            ["Schedar"] = "Schedar",
            ["Gacrux"] = "Gacrux",
            ["Pulcherrima"] = "Pulcherrima",
            ["Achird"] = "Achird",
            ["Zubenelgenubi"] = "Zubenelgenubi",
            ["Vindemiatrix"] = "Vindemiatrix",
            ["Sadachbia"] = "Sadachbia",
            ["Sadaltager"] = "Sadaltager",
            ["Sulafat"] = "Sulafat",
        };

    public static string? Normalize(string? voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName))
        {
            return null;
        }

        return CanonicalVoiceNames.TryGetValue(voiceName.Trim(), out var canonicalVoiceName)
            ? canonicalVoiceName
            : null;
    }

    public static string NormalizeOrDefault(string? voiceName)
    {
        return Normalize(voiceName) ?? DefaultVoiceName;
    }

    public static bool IsSupported(string? voiceName)
    {
        return Normalize(voiceName) is not null;
    }
}