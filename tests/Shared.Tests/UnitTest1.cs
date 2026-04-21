using AxonVoiceAI.Shared.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AxonVoiceAI.Shared.Tests;

public sealed class RequiredConfigurationExtensionsTests
{
    [Fact]
    public void GetRequiredValue_UsesPrimaryKey_WhenPresent()
    {
        var configuration = BuildConfiguration(("JWT_SIGNING_KEY", "primary-value"));

        var value = configuration.GetRequiredValue(new TestHostEnvironment("Production"), "JWT_SIGNING_KEY", "LEGACY_JWT_SIGNING_KEY");

        value.Should().Be("primary-value");
    }

    [Fact]
    public void GetRequiredValue_UsesAlias_WhenPrimaryKeyMissing()
    {
        var configuration = BuildConfiguration(("POSTGRES_URL", "Host=localhost;Database=voiceagent"));

        var value = configuration.GetRequiredValue(new TestHostEnvironment("Production"), "POSTGRES_CONNECTION_STRING", "POSTGRES_URL");

        value.Should().Be("Host=localhost;Database=voiceagent");
    }

    [Fact]
    public void GetRequiredValue_InDevelopment_IncludesLocalSetupGuidance()
    {
        var configuration = BuildConfiguration();

        var action = () => configuration.GetRequiredValue(new TestHostEnvironment(Environments.Development), "JWT_SIGNING_KEY", "LEGACY_JWT_SIGNING_KEY");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT_SIGNING_KEY is required. For local development, set it with .NET User Secrets or an environment variable. Docker Compose reads values from .env, but dotnet run does not. Accepted aliases: LEGACY_JWT_SIGNING_KEY.");
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefault_WhenKeysAreMissing()
    {
        var configuration = BuildConfiguration();

        var value = configuration.GetValueOrDefault("llama3.2", "OLLAMA_SUMMARY_MODEL", "OLLAMA_COMPLETION_MODEL");

        value.Should().Be("llama3.2");
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        var initialData = values.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AxonVoiceAI.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
