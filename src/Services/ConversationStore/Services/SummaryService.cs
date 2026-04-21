using AxonVoiceAI.ConversationStore.Data.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AxonVoiceAI.ConversationStore.Services;

/// <summary>
/// Calls Ollama post-session to generate a concise summary of the conversation transcript.
/// </summary>
public sealed class SummaryService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<SummaryService> _logger;

    public SummaryService(HttpClient httpClient, string model, ILogger<SummaryService> logger)
    {
        _httpClient = httpClient;
        _model = model;
        _logger = logger;
    }

    public async Task<string?> SummarizeSessionAsync(
        ConversationSession session,
        IReadOnlyList<SessionFunctionCall> functionCalls,
        string conversationTranscript,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conversationTranscript))
            return null;

        var functionCallsSummary = functionCalls.Count > 0
            ? string.Join("\n", functionCalls.Select(f =>
                $"- {f.FunctionName}: {(f.Succeeded ? "succeeded" : "failed")} in {f.DurationMs}ms"))
            : "No function calls made.";

        var prompt = $"""
            Summarize the following voice call session in 2-3 sentences.
            Language: {session.Language}
            Duration: {session.DurationSeconds} seconds

            Function calls:
            {functionCallsSummary}

            Transcript:
            {conversationTranscript}

            Provide only the summary, no additional commentary.
            """;

        try
        {
            var request = new OllamaGenerateRequest(_model, prompt, Stream: false);
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
            return body?.Response?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate summary for session {SessionId}.", session.Id);
            return null;
        }
    }

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string Response);
}
