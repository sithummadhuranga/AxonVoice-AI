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
        AppendOrderSection(parts, agentConfig);
        AppendWorkflowBoundariesSection(parts, agentConfig);
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
        parts.AppendLine("Sound warm, calm, and confident — not robotic, theatrical, or overly formal.");
        parts.AppendLine("Use short spoken sentences. Respond immediately when the caller's intent is clear.");
        parts.AppendLine("Do not repeat the caller's words or confirmed details except in the final summary.");
        parts.AppendLine("Do not say 'according to the context', 'as an AI', or 'I am checking the database'.");
        parts.AppendLine($"Before calling any tool, always speak a brief acknowledgment first: Sinhala → '{AcknowledgmentPhrases.ForLanguage("si")}', Tamil → '{AcknowledgmentPhrases.ForLanguage("ta")}', English → '{AcknowledgmentPhrases.ForLanguage("en")}'. Match the current conversation language. Never say an English acknowledgment in a Sinhala or Tamil session.");
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
        parts.AppendLine(hasBootstrapKnowledge
            ? "The knowledge excerpts above are a partial snapshot — call search_knowledge_base for factual questions about products, services, pricing, opening hours, location, policies, delivery, appointment types, ticket rules, or other business-specific details unless the exact answer is already in the current context."
            : "No knowledge excerpts were pre-loaded — call search_knowledge_base for factual business questions about products, services, pricing, opening hours, location, policies, delivery, appointment types, ticket rules, or other business-specific details.");
        parts.AppendLine("Return business facts only. Never mention chunk IDs, source filenames, or that a tool was used.");
        parts.AppendLine("If search_knowledge_base returns no matches, tell the caller you were unable to find that information.");
        parts.AppendLine();
    }

    private static void AppendBookingSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        if (!agentConfig.BookingEnabled)
            return;

        parts.AppendLine("## Booking Rules");
        parts.AppendLine("You handle reservations and appointments using a strict two-step sequence: first check_availability, then create_pending_booking.");
        parts.AppendLine("Collect all required details one at a time before calling any tool: full name, phone number, date, time, and the slot quantity such as party size, attendee count, or seat count.");
        parts.AppendLine("Use partySize as that slot quantity when calling the booking tools.");
        parts.AppendLine("Once you have all details, confirm them with the caller in a single summary before doing anything else.");
        parts.AppendLine();
        parts.AppendLine("Step 1 — Availability check:");
        parts.AppendLine("Call check_availability exactly once with the confirmed date, time, and party size.");
        parts.AppendLine("If available: proceed to step 2. Do NOT ask for details again. Do NOT restart the booking flow.");
        parts.AppendLine("If unavailable: apologise briefly, suggest one or two alternative slots, and ask the caller to confirm an alternative. Do not call create_pending_booking for an unavailable slot.");
        parts.AppendLine();
        parts.AppendLine("Step 2 — Create booking:");
        parts.AppendLine("Call create_pending_booking exactly once using the already collected name, phone, date, time, and party size.");
        parts.AppendLine("Do NOT call create_pending_booking a second time unless the caller explicitly changes a detail and asks to proceed again.");
        parts.AppendLine("After a successful booking response, read out the confirmation code and stop. The booking flow is complete.");
        parts.AppendLine("If the booking tool returns an error, apologise once and ask whether the caller wants to try again. Do not loop automatically.");
        parts.AppendLine("Never say the reservation, appointment, or booking is confirmed until the tool returns a successful confirmation code.");
        parts.AppendLine();
    }

    private static void AppendOrderSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        if (!agentConfig.OrderingEnabled)
            return;

        parts.AppendLine("## Order Rules");
        parts.AppendLine("Collect orders in this sequence: item names and quantities → delivery or pickup → customer name and phone number.");
        parts.AppendLine("Before calling place_order, read back the complete order with item names, quantities, and total amount, then ask the caller to confirm.");
        parts.AppendLine("Call place_order exactly once after the caller confirms. Read out the confirmation code and estimated ready time. The order flow is complete.");
        parts.AppendLine("Do not call place_order more than once for the same order. If it returns an error, apologise once and ask whether to try again.");
        parts.AppendLine();
    }

    private static void AppendWorkflowBoundariesSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        parts.AppendLine("## Workflow Boundaries");
        parts.AppendLine("Only offer actions that match the enabled tools and confirmed business facts in the current session.");

        if (!agentConfig.BookingEnabled)
            parts.AppendLine("Do not promise reservations, appointments, seats, tickets, or time slots because booking tools are not enabled.");

        if (!agentConfig.OrderingEnabled)
            parts.AppendLine("Do not take or confirm orders, purchases, or delivery requests because ordering tools are not enabled.");

        parts.AppendLine();
    }

    private static void AppendLanguageSection(StringBuilder parts, AgentConfigDto agentConfig)
    {
        var primaryLanguage = DescribeLanguage(agentConfig.Language);
        var supportedList = DescribeSupportedLanguages(agentConfig.SupportedLanguages);

        parts.AppendLine("## Language Rules");
        parts.AppendLine($"Start speaking in {primaryLanguage} immediately from the first word of every session.");
        parts.AppendLine($"If the caller responds in a different supported language ({supportedList}), switch immediately and continue in it for the rest of the session.");
        parts.AppendLine($"If the language is ambiguous after two exchanges, continue in {primaryLanguage}.");
        parts.AppendLine("If the caller asks for an unsupported language, state the supported languages and ask which they prefer.");
        parts.AppendLine("Use natural everyday spoken phrasing. For Sinhala and Tamil, avoid stiff written style and English filler words.");
        parts.AppendLine("Voice conversation only: short sentences, no bullet points, no markdown.");
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
                Description = "Search the uploaded business knowledge base for factual information such as products, services, pricing, opening hours, location, delivery details, policies, appointment types, or ticket rules.",
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
                    Description = "Check whether a specific date, time, and slot quantity is available for a reservation, appointment, seat, or ticketed slot.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            date = new { type = "string", description = "Date in YYYY-MM-DD format" },
                            time = new { type = "string", description = "Time in HH:mm format (24-hour)" },
                            partySize = new { type = "integer", description = "Number of people, seats, attendees, or units attached to the slot" },
                        },
                        required = new[] { "date", "time", "partySize" }
                    }
                },
                new GeminiFunctionDeclaration
                {
                    Name = "create_pending_booking",
                    Description = "Create a pending reservation, appointment, or slot hold after availability is confirmed.",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            date = new { type = "string", description = "Date in YYYY-MM-DD format" },
                            time = new { type = "string", description = "Time in HH:mm format (24-hour)" },
                            partySize = new { type = "integer", description = "Number of people, seats, attendees, or units attached to the slot" },
                            customerName = new { type = "string", description = "Full name of the customer" },
                            customerPhone = new { type = "string", description = "Customer's phone number" },
                            specialRequests = new { type = "string", description = "Any special requests or notes" },
                        },
                        required = new[] { "date", "time", "partySize", "customerName", "customerPhone" }
                    }
                }
            ]);
        }

        if (agentConfig.OrderingEnabled)
        {
            functionDeclarations.Add(new GeminiFunctionDeclaration
            {
                Name = "place_order",
                Description = "Record a verbally confirmed customer order after all items and the total have been agreed.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        itemsJson = new { type = "string", description = "JSON array of items: [{\"item\":\"name\",\"qty\":2,\"price\":450}]" },
                        customerName = new { type = "string", description = "Customer full name" },
                        customerPhone = new { type = "string", description = "Customer phone number" },
                        orderType = new { type = "string", description = "'delivery' or 'pickup'", @enum = new[] { "delivery", "pickup" } },
                        deliveryAddress = new { type = "string", description = "Street address — required for delivery orders" },
                    },
                    required = new[] { "itemsJson", "customerName", "customerPhone", "orderType" }
                }
            });
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
