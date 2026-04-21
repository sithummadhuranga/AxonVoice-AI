namespace AxonVoiceAI.Shared.DTOs;

public record AgentConfigDto(
    Guid AgentId,
    Guid TenantId,
    string AgentName,
    string BusinessName,
    string Persona,
    string Language,
    string[] SupportedLanguages,
    string VoiceName,
    string GeminiModel,
    string GeminiApiKey,
    bool BookingEnabled,
    int SessionTimeoutSeconds,
    int SilenceTimeoutSeconds);

public record BusinessHoursDto(
    Guid Id,
    int DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    int SlotDurationMinutes,
    int MaxCapacityPerSlot);

public record KnowledgeChunkDto(
    string Text,
    string SourceFilename,
    int ChunkIndex,
    float Score);

public record SessionStartContextDto(
    Guid TenantId,
    Guid AgentId,
    Guid SessionId,
    string Language);

