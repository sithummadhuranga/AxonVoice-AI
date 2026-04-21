using AxonVoiceAI.Shared.Contracts;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AxonVoiceAI.KnowledgeBase.Providers;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    // Architecture specifies batching in groups of 20 to avoid hammering Ollama.
    private const int BatchSize = 20;

    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        string model,
        ILogger<OllamaEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _model = model;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var results = await EmbedBatchAsync([text], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct)
    {
        var allEmbeddings = new List<float[]>(texts.Count);

        for (var offset = 0; offset < texts.Count; offset += BatchSize)
        {
            var batch = texts.Skip(offset).Take(BatchSize).ToList();
            var batchEmbeddings = await EmbedSingleBatchAsync(batch, ct);
            allEmbeddings.AddRange(batchEmbeddings);
        }

        return allEmbeddings.AsReadOnly();
    }

    private async Task<IReadOnlyList<float[]>> EmbedSingleBatchAsync(
        IReadOnlyList<string> batch,
        CancellationToken ct)
    {
        var results = new List<float[]>(batch.Count);

        foreach (var text in batch)
        {
            var request = new OllamaEmbeddingRequest(_model, text);
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(ct)
                ?? throw new InvalidOperationException("Ollama returned an empty embedding response.");

            results.Add(body.Embedding);
        }

        return results.AsReadOnly();
    }

    private sealed record OllamaEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
