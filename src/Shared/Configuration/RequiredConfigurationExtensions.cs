using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AxonVoiceAI.Shared.Configuration;

public static class RequiredConfigurationExtensions
{
    public static string GetRequiredValue(
        this IConfiguration configuration,
        IHostEnvironment environment,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (keys.Length == 0)
            throw new ArgumentException("At least one configuration key must be provided.", nameof(keys));

        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        throw new InvalidOperationException(BuildMissingConfigurationMessage(environment, keys));
    }

    public static string GetValueOrDefault(
        this IConfiguration configuration,
        string defaultValue,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return defaultValue;
    }

    private static string BuildMissingConfigurationMessage(IHostEnvironment environment, IReadOnlyList<string> keys)
    {
        var primaryKey = keys[0];
        if (!environment.IsDevelopment())
            return $"{primaryKey} is required.";

        var aliasText = keys.Count > 1
            ? $" Accepted aliases: {string.Join(", ", keys.Skip(1))}."
            : string.Empty;

        return $"{primaryKey} is required. For local development, set it with .NET User Secrets or an environment variable. Docker Compose reads values from .env, but dotnet run does not.{aliasText}";
    }
}