using AxonVoiceAI.Shared;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.SessionRelay.Gemini;
using System.Text;

namespace AxonVoiceAI.SessionRelay.Prompts;

public static class SystemPromptAssembler
{
    public static GeminiSessionConfig BuildSessionConfig(
        AgentConfigDto agentConfig,
        IReadOnlyList<KnowledgeChunkDto> knowledgeChunks)
    {
        var systemPrompt = AssembleSystemPrompt(agentConfig, knowledgeChunks);

        return new GeminiSessionConfig
        {
            Setup = new GeminiSetup
            {
                Model = NormalizeModelName(agentConfig.GeminiModel),
                GenerationConfig = CreateGenerationConfig(agentConfig.VoiceName),
                SystemInstruction = new GeminiSystemInstruction
                {
                    Parts = [new GeminiTextPart { Text = systemPrompt }]
                },
                Tools = BuildToolDeclarations(agentConfig),
            }
        };
    }

    private static string AssembleSystemPrompt(
        AgentConfigDto agentConfig,
        IReadOnlyList<KnowledgeChunkDto> knowledgeChunks)
    {
        var parts = new StringBuilder();
        AppendIdentitySection(parts, agentConfig);
        AppendKnowledgeSection(parts, knowledgeChunks);
        AppendBookingSection(parts, agentConfig);
        AppendLanguageSection(parts, agentConfig);

        return parts.ToString().Trim();
    }

    private static void AppendIdentitySection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        parts.AppendLine("## Identity");
        parts.AppendLine($"You are {agentConfig.AgentName}, a live voice assistant for {agentConfig.BusinessName}.");
        if (!string.IsNullOrWhiteSpace(agentConfig.Persona))
            parts.AppendLine(agentConfig.Persona);
        parts.AppendLine("Speak like a real person. Keep responses concise, natural, and appropriate for a voice conversation.");
        parts.AppendLine();
    }

    private static void AppendKnowledgeSection(StringBuilder parts, IReadOnlyList<KnowledgeChunkDto> knowledgeChunks)
    {
        if (knowledgeChunks.Count == 0)
            return;

        parts.AppendLine("## Knowledge Base");
        parts.AppendLine("--- KNOWLEDGE BASE ---");
        foreach (var chunk in knowledgeChunks)
        {
            parts.AppendLine($"[Source: {chunk.SourceFilename} | Chunk: {chunk.ChunkIndex}]");
            parts.AppendLine(chunk.Text);
            parts.AppendLine();
        }
        parts.AppendLine("--- END KNOWLEDGE BASE ---");
        parts.AppendLine("Answer questions using the knowledge base when it is relevant. If the answer is not in the provided knowledge, say you will check rather than invent details.");
        parts.AppendLine();
    }

    private static void AppendBookingSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        if (!agentConfig.BookingEnabled)
            return;

        parts.AppendLine("## Booking Rules");
        parts.AppendLine("You can use tools to check availability and create pending bookings.");
        parts.AppendLine("Always confirm the caller's full name, phone number, date, time, and party size before creating a booking.");
        parts.AppendLine("Use check_availability before create_pending_booking.");
        parts.AppendLine("Never say a booking is confirmed unless the booking tool succeeds.");
        parts.AppendLine();
    }

    private static void AppendLanguageSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        parts.AppendLine("## Language Rules");
        parts.AppendLine($"This agent supports: {DescribeSupportedLanguages(agentConfig.SupportedLanguages)}.");
        parts.AppendLine("Detect the caller's language from their first substantial utterance.");
        parts.AppendLine($"If the language is unclear, default to {DescribeLanguage(agentConfig.Language)}.");
        parts.AppendLine("Continue in the same language for the rest of the session unless the caller explicitly asks to change languages.");
        parts.AppendLine("If the caller asks for an unsupported language, clearly state the supported languages and continue in the supported language they choose.");
        parts.AppendLine("This is a voice conversation. Use short sentences and avoid bullets, numbering, or markdown.");
    }

    private static GeminiGenerationConfig CreateGenerationConfig(string voiceName)
    {
        return new GeminiGenerationConfig
        {
            SpeechConfig = new GeminiSpeechConfig
            {
                VoiceConfig = new GeminiVoiceConfig
                {
                    PrebuiltVoiceConfig = new GeminiPrebuiltVoice
                    {
                        VoiceName = string.IsNullOrWhiteSpace(voiceName) ? "Aoede" : voiceName
                    }
                }
            }
        };
    }

    private static string NormalizeModelName(string geminiModel)
    {
        if (string.IsNullOrWhiteSpace(geminiModel))
            return "models/gemini-2.0-flash-live-001";

        return geminiModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? geminiModel
            : $"models/{geminiModel}";
    }

    private static string DescribeSupportedLanguages(string[] supportedLanguages)
    {
        var languages = supportedLanguages.Length == 0 ? ["en"] : supportedLanguages;
        return string.Join(", ", languages.Select(DescribeLanguage));
    }

    private static string DescribeLanguage(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant() switch
        {
            "si" => "Sinhala",
            "ta" => "Tamil",
            "en" => "English",
            _ => languageCode,
        };
    }

    private static IReadOnlyList<GeminiToolDeclaration> BuildToolDeclarations(AgentConfigDto agentConfig)
    {
        if (!agentConfig.BookingEnabled)
            return [];

        return
        [
            new GeminiToolDeclaration
            {
                FunctionDeclarations =
                [
                    new GeminiFunctionDeclaration
                    {
                        Name = "check_availability",
                        Description = "Check whether a specific date, time, and party size is available for booking.",
                        Parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                date = new { type = "string", description = "Date in YYYY-MM-DD format" },
                                time = new { type = "string", description = "Time in HH:mm format (24-hour)" },
                                partySize = new { type = "integer", description = "Number of people" },
                            },
                            required = new[] { "date", "time", "partySize" }
                        }
                    },
                    new GeminiFunctionDeclaration
                    {
                        Name = "create_pending_booking",
                        Description = "Create a pending booking after availability is confirmed.",
                        Parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                date = new { type = "string", description = "Date in YYYY-MM-DD format" },
                                time = new { type = "string", description = "Time in HH:mm format (24-hour)" },
                                partySize = new { type = "integer", description = "Number of people" },
                                customerName = new { type = "string", description = "Full name of the customer" },
                                customerPhone = new { type = "string", description = "Customer's phone number" },
                                specialRequests = new { type = "string", description = "Any special requests or notes" },
                            },
                            required = new[] { "date", "time", "partySize", "customerName", "customerPhone" }
                        }
                    }
                ]
            }
        ];
    }
}
