namespace AxonVoiceAI.KnowledgeBase.Ingestion;

/// <summary>
/// Splits extracted document text into overlapping chunks suitable for embedding.
/// Target: 400 tokens (~1600 chars), 10% overlap (~160 chars), minimum 50 tokens (~200 chars).
/// </summary>
public static class TextChunker
{
    private const int TargetChunkLength = 1600;
    private const int OverlapLength = 160;
    private const int MinimumChunkLength = 200;

    public static IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var normalized = NormalizeWhitespace(text);
        var chunks = new List<string>();
        var position = 0;

        while (position < normalized.Length)
        {
            var end = Math.Min(position + TargetChunkLength, normalized.Length);

            // Try to break at a sentence boundary or whitespace to avoid cutting words.
            if (end < normalized.Length)
            {
                var boundary = FindBreakPoint(normalized, position, end);
                end = boundary > position ? boundary : end;
            }

            var chunk = normalized[position..end].Trim();
            if (chunk.Length >= MinimumChunkLength)
                chunks.Add(chunk);

            if (end >= normalized.Length)
                break;

            // Advance by target minus overlap to create sliding window.
            position = Math.Max(end - OverlapLength, position + 1);
        }

        return chunks.AsReadOnly();
    }

    private static int FindBreakPoint(string text, int start, int preferredEnd)
    {
        // Prefer sentence-ending punctuation within the last 200 chars of the window.
        var searchStart = Math.Max(start, preferredEnd - 200);
        for (var i = preferredEnd; i >= searchStart; i--)
        {
            if (i < text.Length && (text[i] == '.' || text[i] == '!' || text[i] == '?') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                return i + 1;
        }

        // Fall back to whitespace break.
        for (var i = preferredEnd; i >= searchStart; i--)
        {
            if (i < text.Length && char.IsWhiteSpace(text[i]))
                return i;
        }

        return preferredEnd;
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var lastWasWhitespace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(c);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }
}
