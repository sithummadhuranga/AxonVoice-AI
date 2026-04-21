using AxonVoiceAI.Shared;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.SessionRelay.Gemini;

namespace AxonVoiceAI.SessionRelay.Prompts;

/// <summary>
/// Assembles the four-part system prompt from agent configuration and retrieved knowledge.
/// Structure: [Identity] → [Knowledge] → [Booking Rules] → [Language Instructions]
/// </summary>
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
        var parts = new System.Text.StringBuilder();

        // Part 1: Identity
        parts.AppendLine("## Identity");
        parts.AppendLine($"You are {agentConfig.AgentName}, a voice assistant for {agentConfig.BusinessName}.");
        if (!string.IsNullOrWhiteSpace(agentConfig.Persona))
            parts.AppendLine(agentConfig.Persona);
        parts.AppendLine();

        // Part 2: Knowledge base context
        if (knowledgeChunks.Count > 0)
        {
            parts.AppendLine("## Knowledge Base");
            parts.AppendLine("Use the following information to answer questions:");
            parts.AppendLine();
            foreach (var chunk in knowledgeChunks)
            {
                parts.AppendLine($"[Source: {chunk.SourceFilename}]");
                parts.AppendLine(chunk.Text);
                parts.AppendLine();
            }
        }

        // Part 3: Booking and availability rules
        if (agentConfig.BookingEnabled)
        {
            parts.AppendLine("## Booking Rules");
            parts.AppendLine("You can check availability and create pending bookings on behalf of callers.");
            parts.AppendLine("Always confirm the caller's name, contact number, date, time, and party size before making a booking.");
            parts.AppendLine("Use the check_availability function before attempting to create a booking.");
            parts.AppendLine();
        }

        // Part 4: Language instructions
        parts.AppendLine("## Language");
        parts.AppendLine($"The caller's preferred language is: {agentConfig.Language}.");
        parts.AppendLine("Respond in the same language the caller uses. If they switch language, follow them.");
        parts.AppendLine("Be natural, conversational, and concise. This is a voice call — do not use lists or formatting.");

        return parts.ToString().Trim();
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
                                party_size = new { type = "integer", description = "Number of people" },
                            },
                            required = new[] { "date", "time", "party_size" }
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
                                party_size = new { type = "integer", description = "Number of people" },
                                customer_name = new { type = "string", description = "Full name of the customer" },
                                contact_number = new { type = "string", description = "Customer's phone number" },
                                notes = new { type = "string", description = "Any special requests or notes" },
                            },
                            required = new[] { "date", "time", "party_size", "customer_name", "contact_number" }
                        }
                    }
                ]
            }
        ];
    }
}
