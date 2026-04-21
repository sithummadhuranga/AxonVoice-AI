namespace AxonVoiceAI.Shared.DTOs;

public record SessionTokenResponse(
    string Token,
    string WsUrl,
    DateTimeOffset ExpiresAt);

public record SessionTokenRequest(
    Guid AgentId,
    string Channel);

public record SessionCloseRequest(
    Guid SessionId,
    string CloseReason,
    string? DetectedLanguage,
    int DurationSeconds);
