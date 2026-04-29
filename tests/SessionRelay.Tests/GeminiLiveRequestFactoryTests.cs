using System.Text.Json;
using AxonVoiceAI.SessionRelay.Gemini;
using AxonVoiceAI.SessionRelay.Handlers;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class GeminiLiveRequestFactoryTests
{
    [Fact]
    public void SessionSetup_SerializesUsingLiveApiCamelCaseContract()
    {
        var sessionConfig = new GeminiSessionConfig
        {
            Setup = new GeminiSetup
            {
                Model = "models/gemini-2.5-flash-native-audio-preview-12-2025",
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseModalities = ["AUDIO"],
                    SpeechConfig = new GeminiSpeechConfig
                    {
                        VoiceConfig = new GeminiVoiceConfig
                        {
                            PrebuiltVoiceConfig = new GeminiPrebuiltVoice
                            {
                                VoiceName = "Puck"
                            }
                        }
                    }
                },
                SystemInstruction = new GeminiSystemInstruction
                {
                    Parts = [new GeminiTextPart { Text = "Speak naturally." }]
                },
                Tools =
                [
                    new GeminiToolDeclaration
                    {
                        FunctionDeclarations =
                        [
                            new GeminiFunctionDeclaration
                            {
                                Name = "check_availability",
                                Description = "Checks availability.",
                                Parameters = new { type = "object" }
                            }
                        ]
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(sessionConfig);

        json.Should().Contain("\"generationConfig\"");
        json.Should().Contain("\"systemInstruction\"");
        json.Should().Contain("\"responseModalities\"");
        json.Should().Contain("\"speechConfig\"");
        json.Should().Contain("\"thinkingConfig\"");
        json.Should().Contain("\"thinkingBudget\":0");
        json.Should().Contain("\"voiceConfig\"");
        json.Should().Contain("\"prebuiltVoiceConfig\"");
        json.Should().Contain("\"voiceName\":\"Puck\"");
        json.Should().Contain("\"functionDeclarations\"");
        json.Should().NotContain("generation_config");
        json.Should().NotContain("system_instruction");
        json.Should().NotContain("response_modalities");
        json.Should().NotContain("speech_config");
        json.Should().NotContain("voice_config");
        json.Should().NotContain("prebuilt_voice_config");
        json.Should().NotContain("voice_name");
        json.Should().NotContain("function_declarations");
    }

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
    public void CreateClientContentTextTurn_WrapsInstructionInUserTurn()
    {
        var request = GeminiLiveRequestFactory.CreateClientContentTextTurn(
            "Speak this exact acknowledgment to the caller and add nothing else: \"One moment, let me check that for you.\"");

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"clientContent\"");
        json.Should().Contain("\"turns\"");
        json.Should().Contain("\"role\":\"user\"");
        json.Should().Contain("\"turnComplete\":true");
        json.Should().Contain("Speak this exact acknowledgment to the caller");
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