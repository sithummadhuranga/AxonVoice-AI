namespace AxonVoiceAI.Shared.Contracts;

public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

public interface ITextCompletionProvider
{
    Task<string> CompleteAsync(string prompt, int maxTokens, CancellationToken ct);
}
