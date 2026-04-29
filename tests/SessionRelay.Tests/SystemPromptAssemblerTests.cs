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
        sessionConfig.Setup.GenerationConfig.ThinkingConfig.ThinkingBudget.Should().Be(0);
        toolSchemaJson.Should().Contain("search_knowledge_base");
        toolSchemaJson.Should().Contain("\"query\"");
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
        prompt.Should().Contain("Start speaking in Sinhala immediately from the first word of every session.");
        prompt.Should().Contain("If the language is ambiguous after two exchanges, continue in Sinhala.");
        prompt.Should().Contain("If the caller responds in a different supported language (Sinhala, Tamil, English)");
        prompt.Should().Contain("search_knowledge_base");
        prompt.Should().Contain("menu and price questions");
        prompt.Should().Contain("Before calling ANY tool, ALWAYS speak a brief acknowledgment");
        prompt.Should().Contain("After a successful booking response, read out the confirmation code and stop.");
        prompt.Should().Contain("Use natural spoken language throughout. For Sinhala and Tamil");
        prompt.Should().Contain("Call check_availability exactly once");
    }

    [Fact]
    public void BuildSessionConfig_BookingDisabled_StillExposesKnowledgeSearchTool()
    {
        var agentConfig = CreateAgentConfig(bookingEnabled: false);

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, []);
        var toolSchemaJson = JsonSerializer.Serialize(sessionConfig.Setup.Tools);

        toolSchemaJson.Should().Contain("search_knowledge_base");
        toolSchemaJson.Should().NotContain("check_availability");
        toolSchemaJson.Should().NotContain("create_pending_booking");
    }

    [Fact]
    public void BuildSessionConfig_InvalidVoiceName_FallsBackToDefaultVoice()
    {
        var agentConfig = CreateAgentConfig() with { VoiceName = "UnknownVoice" };

        var sessionConfig = SystemPromptAssembler.BuildSessionConfig(agentConfig, []);

        sessionConfig.Setup.GenerationConfig.SpeechConfig.VoiceConfig.PrebuiltVoiceConfig.VoiceName.Should().Be("Sulafat");
    }

    private static AgentConfigDto CreateAgentConfig(bool bookingEnabled = true)
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
            BookingEnabled: bookingEnabled,
            SessionTimeoutSeconds: 600,
            SilenceTimeoutSeconds: 90);
    }
}