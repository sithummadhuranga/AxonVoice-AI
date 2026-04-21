using System.Text.Json;
using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.SessionRelay.Handlers;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class GeminiLiveRequestFactoryTests
{
    [Fact]
    public void CreateAudioInput_UsesRealtimeAudioPayload()
    {
        var request = GeminiLiveRequestFactory.CreateAudioInput([0x01, 0x02, 0x03]);
        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"realtimeInput\"");
        json.Should().Contain("\"audio\"");
        json.Should().Contain("\"mimeType\":\"audio/pcm;rate=16000\"");
        json.Should().Contain(Convert.ToBase64String([0x01, 0x02, 0x03]));
        json.Should().NotContain("mediaChunks");
    }

    [Fact]
    public void CreateToolResponse_WrapsFunctionResultsForLiveApi()
    {
        var request = GeminiLiveRequestFactory.CreateToolResponse(
        [
            new GeminiFunctionResponse(
                "call-1",
                "check_availability",
                "{\"available\":true,\"remainingCapacity\":6}")
        ]);

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"toolResponse\"");
        json.Should().Contain("\"functionResponses\"");
        json.Should().Contain("\"id\":\"call-1\"");
        json.Should().Contain("\"name\":\"check_availability\"");
        json.Should().Contain("\"response\":{");
        json.Should().Contain("\"result\":{");
        json.Should().Contain("\"available\":true");
        json.Should().Contain("\"remainingCapacity\":6");
    }

        [Fact]
        public void GeminiMessage_DeserializesToolCallCancellationIds()
        {
                const string json = """
                        {
                            "toolCallCancellation": {
                                "ids": ["call-1", "call-2"]
                            }
                        }
                        """;

                var message = JsonSerializer.Deserialize<GeminiMessage>(json);

                message.Should().NotBeNull();
                message!.ToolCallCancellation.Should().NotBeNull();
                message.ToolCallCancellation!.Ids.Should().Equal("call-1", "call-2");
        }
}