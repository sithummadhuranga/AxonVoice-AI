namespace AxonVoiceAI.AgentConfig.Configuration;

public sealed class AgentConfigOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string RedisConnectionString { get; set; } = string.Empty;
    public string PlatformMasterKey { get; set; } = string.Empty;
    public string JwtSigningKey { get; set; } = string.Empty;
    public string PlatformBaseUrl { get; set; } = string.Empty;
}
