using System.Text.Json;
using System.Text.Json.Serialization;
using AxonVoiceAI.SessionRelay.Handlers;

namespace AxonVoiceAI.SessionRelay.Gemini;

internal static class GeminiLiveRequestFactory
{
    public static GeminiClientMessage CreateAudioInput(byte[] pcmData)
    {
        return new GeminiClientMessage
        {
            RealtimeInput = new GeminiRealtimeInput
            {
                Audio = new GeminiBlob
                {
                    MimeType = "audio/pcm;rate=16000",
                    Data = Convert.ToBase64String(pcmData)
                }
            }
        };
    }

    public static GeminiClientMessage CreateAudioStreamEnd()
    {
        return new GeminiClientMessage
        {
            RealtimeInput = new GeminiRealtimeInput
            {
                AudioStreamEnd = true,
            }
        };
    }

    public static GeminiClientMessage CreateClientContentTextTurn(string text)
    {
        return new GeminiClientMessage
        {
            ClientContent = new GeminiClientContent
            {
                Turns =
                [
                    new GeminiConversationTurn
                    {
                        Role = "user",
                        Parts =
                        [
                            new GeminiConversationPart
                            {
                                Text = text,
                            }
                        ],
                    }
                ],
                TurnComplete = true,
            }
        };
    }

    public static GeminiClientMessage CreateToolResponse(IReadOnlyList<GeminiFunctionResponse> responses)
    {
        return new GeminiClientMessage
        {
            ToolResponse = new GeminiToolResponseEnvelope
            {
                FunctionResponses = responses.Select(CreateFunctionResponse).ToArray()
            }
        };
    }

    private static GeminiToolFunctionResponse CreateFunctionResponse(GeminiFunctionResponse response)
    {
        var result = JsonSerializer.Deserialize<JsonElement>(response.ResponseJson);

        return new GeminiToolFunctionResponse
        {
            Id = response.CallId,
            Name = response.FunctionName,
            Response = new GeminiToolFunctionResponseBody
            {
                Result = result
            }
        };
    }
}

internal sealed record GeminiClientMessage
{
    [JsonPropertyName("setup")]
    public GeminiSetup? Setup { get; init; }

    [JsonPropertyName("clientContent")]
    public GeminiClientContent? ClientContent { get; init; }

    [JsonPropertyName("realtimeInput")]
    public GeminiRealtimeInput? RealtimeInput { get; init; }

    [JsonPropertyName("toolResponse")]
    public GeminiToolResponseEnvelope? ToolResponse { get; init; }
}

internal sealed record GeminiClientContent
{
    [JsonPropertyName("turns")]
    public IReadOnlyList<GeminiConversationTurn> Turns { get; init; } = [];

    [JsonPropertyName("turnComplete")]
    public bool? TurnComplete { get; init; }
}

internal sealed record GeminiConversationTurn
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiConversationPart> Parts { get; init; } = [];
}

internal sealed record GeminiConversationPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

internal sealed record GeminiRealtimeInput
{
    [JsonPropertyName("audio")]
    public GeminiBlob? Audio { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("audioStreamEnd")]
    public bool? AudioStreamEnd { get; init; }
}

internal sealed record GeminiBlob
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;
}

internal sealed record GeminiToolResponseEnvelope
{
    [JsonPropertyName("functionResponses")]
    public IReadOnlyList<GeminiToolFunctionResponse> FunctionResponses { get; init; } = [];
}

internal sealed record GeminiToolFunctionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("response")]
    public GeminiToolFunctionResponseBody Response { get; init; } = null!;
}

internal sealed record GeminiToolFunctionResponseBody
{
    [JsonPropertyName("result")]
    public JsonElement Result { get; init; }
}