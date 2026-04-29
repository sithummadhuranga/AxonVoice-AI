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
        AppendSpeechStyleSection(parts);
        AppendKnowledgeSection(parts, knowledgeChunks);
        AppendKnowledgeToolRules(parts, knowledgeChunks.Count > 0);
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
        parts.AppendLine("Speak like a real person in a live phone-style conversation.");
        parts.AppendLine("Keep responses concise, natural, and immediately useful.");
        parts.AppendLine();
    }

    private static void AppendSpeechStyleSection(StringBuilder parts)
    {
        parts.AppendLine("## Speech Style");
        parts.AppendLine("Sound warm, calm, and confident \u2014 not robotic, theatrical, or overly formal.");
        parts.AppendLine("Use short spoken sentences with a steady pace and natural pauses.");
        parts.AppendLine("Start with the answer or the next question directly instead of long preambles.");
        parts.AppendLine("Do not repeat the caller's request, your previous answer, or already confirmed details unless you are giving the final confirmation summary.");
        parts.AppendLine("Do not say phrases like 'according to the context', 'based on the knowledge base', 'as an AI', or 'I am checking the database'.");
        parts.AppendLine("Before calling ANY tool, ALWAYS speak a brief acknowledgment phrase to the caller first \u2014 for example 'One moment, let me check that for you' or the natural equivalent in the caller's language. Speak the phrase, then call the tool. Do not call a tool silently without acknowledging the caller first.");
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

    private static void AppendKnowledgeToolRules(StringBuilder parts, bool hasBootstrapKnowledge)
    {
        parts.AppendLine("## Knowledge Tool Rules");

        if (hasBootstrapKnowledge)
        {
            parts.AppendLine("The knowledge base excerpts above are only the initial context window, not the full document set.");
        }
        else
        {
            parts.AppendLine("No initial knowledge excerpts were loaded into context for this session.");
        }

        parts.AppendLine("When the caller asks for factual business information such as menu items, prices, opening hours, location, delivery details, policies, or services, call search_knowledge_base with a focused query before answering unless the exact answer is already explicit in the current context.");
        parts.AppendLine("Prefer search_knowledge_base for menu and price questions so the answer comes from the uploaded knowledge base rather than memory or guessing.");
        parts.AppendLine("The system may speak a short waiting acknowledgment while a tool is running. Once the tool result arrives, continue with the answer directly and do not repeat the waiting phrase.");
        parts.AppendLine("When search_knowledge_base returns matches, answer with the business facts only. Do not mention chunk numbers, source files, or that a tool was used.");
        parts.AppendLine("If search_knowledge_base returns no relevant matches, clearly say you could not find that information in the business knowledge base.");
        parts.AppendLine();
    }

    private static void AppendBookingSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        if (!agentConfig.BookingEnabled)
            return;

        parts.AppendLine("## Booking Rules");
        parts.AppendLine("You handle bookings using a strict two-step sequence: first check_availability, then create_pending_booking.");
        parts.AppendLine("Collect all required details one at a time before calling any tool: full name, phone number, date, time, and party size.");
        parts.AppendLine("Once you have all details, confirm them with the caller in a single summary before doing anything else.");
        parts.AppendLine();
        parts.AppendLine("Step 1 \u2014 Availability check:");
        parts.AppendLine("Call check_availability exactly once with the confirmed date, time, and party size.");
        parts.AppendLine("If available: proceed to step 2. Do NOT ask for details again. Do NOT restart the booking.");
        parts.AppendLine("If unavailable: apologise briefly, suggest one or two alternative slots, and ask the caller to confirm an alternative. Do not call create_pending_booking for an unavailable slot.");
        parts.AppendLine();
        parts.AppendLine("Step 2 \u2014 Create booking:");
        parts.AppendLine("Call create_pending_booking exactly once using the already collected name, phone, date, time, and party size.");
        parts.AppendLine("Do NOT call create_pending_booking a second time unless the caller explicitly changes a detail and asks to proceed again.");
        parts.AppendLine("After a successful booking response, read out the confirmation code and stop. The booking flow is complete.");
        parts.AppendLine("If the booking tool returns an error, apologise once and ask whether the caller wants to try again. Do not loop automatically.");
        parts.AppendLine("Never say the booking is confirmed until the tool returns a successful confirmation code.");
        parts.AppendLine();
    }

    private static void AppendLanguageSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        var primaryLanguage = DescribeLanguage(agentConfig.Language);
        var supportedList = DescribeSupportedLanguages(agentConfig.SupportedLanguages);

        parts.AppendLine("## Language Rules");
        parts.AppendLine($"Start speaking in {primaryLanguage} immediately from the first word of every session. Do not wait for the caller to speak first to decide the language.");
        parts.AppendLine($"If the caller responds in a different supported language ({supportedList}), switch to that language immediately and continue in it for the rest of the session.");
        parts.AppendLine($"If the language is ambiguous after two exchanges, continue in {primaryLanguage}.");
        parts.AppendLine("If the caller asks for a language that is not in the supported list, clearly state the supported languages and ask which one they prefer.");
        parts.AppendLine("Use natural spoken language throughout. For Sinhala and Tamil, use everyday conversational phrasing, not stiff written formal style.");
        parts.AppendLine("This is a voice conversation. Use short sentences. Never use bullet points, numbering, or markdown formatting.");
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
                        VoiceName = GeminiVoiceCatalog.NormalizeOrDefault(voiceName)
                    }
                }
            }
        };
    }

    private static string NormalizeModelName(string geminiModel)
    {
        if (string.IsNullOrWhiteSpace(geminiModel))
            return "models/gemini-2.5-flash-native-audio-preview-12-2025";

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
        var functionDeclarations = new List<GeminiFunctionDeclaration>
        {
            new()
            {
                Name = "search_knowledge_base",
                Description = "Search the uploaded business knowledge base for factual information such as menu items, prices, business hours, location, delivery details, and policies.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "A focused search query based on the caller's question" },
                    },
                    required = new[] { "query" }
                }
            }
        };

        if (agentConfig.BookingEnabled)
        {
            functionDeclarations.AddRange(
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
            ]);
        }

        return
        [
            new GeminiToolDeclaration
            {
                FunctionDeclarations = functionDeclarations
            }
        ];
    }
}
