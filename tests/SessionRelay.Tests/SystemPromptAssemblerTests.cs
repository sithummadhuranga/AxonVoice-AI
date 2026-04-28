using System.Text.Json;
using AxonVoiceAI.SessionRelay.Prompts;
using AxonVoiceAI.Shared.DTOs;
using FluentAssertions;

namespace AxonVoiceAI.SessionRelay.Tests;

public sealed class SystemPromptAssemblerTests
{
    [Fact]
    public void BuildSessionConfig_AppliesConfiguredModelVoiceAndToolSchema()
    {
        var agentConfig = CreateAgentConfig();

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, []);
        var toolSchemaJson = JsonSerializer.Serialize(sessionConfig.Setup.Tools);

        sessionConfig.Setup.Model.Should().Be("models/gemini-2.5-flash-native-audio-preview-12-2025");
        sessionConfig.Setup.GenerationConfig.SpeechConfig.VoiceConfig.PrebuiltVoiceConfig.VoiceName.Should().Be("Puck");
        toolSchemaJson.Should().Contain("partySize");
        toolSchemaJson.Should().Contain("customerName");
        toolSchemaJson.Should().Contain("customerPhone");
        toolSchemaJson.Should().Contain("specialRequests");
        toolSchemaJson.Should().NotContain("party_size");
        toolSchemaJson.Should().NotContain("customer_name");
        toolSchemaJson.Should().NotContain("contact_number");
    }

    [Fact]
    public void BuildSessionConfig_IncludesKnowledgeAndLockedLanguageRules()
    {
        var agentConfig = CreateAgentConfig();
        var knowledgeChunks = new[]
        {
            new KnowledgeChunkDto("We open at 10 AM every day.", "hours.pdf", 2, 0.91f)
        };

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, knowledgeChunks);
        var prompt = sessionConfig.Setup.SystemInstruction.Parts.Single().Text;

        prompt.Should().Contain("--- KNOWLEDGE BASE ---");
        prompt.Should().Contain("[Source: hours.pdf | Chunk: 2]");
        prompt.Should().Contain("We open at 10 AM every day.");
        prompt.Should().Contain("This agent supports: Sinhala, Tamil, English.");
        prompt.Should().Contain("If the language is unclear, default to Sinhala.");
        prompt.Should().Contain("Continue in the same language for the rest of the session unless the caller explicitly asks to change languages.");
        prompt.Should().Contain("Use check_availability before create_pending_booking.");
    }

    private static AgentConfigDto CreateAgentConfig()
    {
        return new AgentConfigDto(
            AgentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            AgentName: "Maya",
            BusinessName: "Axon Bistro",
            Persona: "You are warm and efficient.",
            Language: "si",
            SupportedLanguages: ["si", "ta", "en"],
            VoiceName: "Puck",
            GeminiModel: "gemini-2.5-flash-native-audio-preview-12-2025",
            GeminiApiKey: "secret",
            BookingEnabled: true,
            SessionTimeoutSeconds: 600,
            SilenceTimeoutSeconds: 90);
    }
}