using AxonVoiceAI.Shared.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AxonVoiceAI.Shared.Tests;

public sealed class PlatformDataProtectionSettingsTests
{
    [Fact]
    public void Resolve_DevelopmentWithoutExplicitSettings_AllowsDefaultHostBehavior()
    {
        var configuration = BuildConfiguration();

        var settings = PlatformDataProtectionSettings.Resolve(
            configuration,
            new TestHostEnvironment(Environments.Development),
            "AxonVoiceAI.Gateway");

        settings.ApplicationName.Should().Be("AxonVoiceAI.Gateway");
        settings.KeysDirectoryPath.Should().BeNull();
        settings.CertificateBase64.Should().BeNull();
        settings.CertificatePassword.Should().BeNull();
    }

    [Fact]
    public void Resolve_ProductionWithoutKeysDirectory_Throws()
    {
        var configuration = BuildConfiguration();

        var action = () => PlatformDataProtectionSettings.Resolve(
            configuration,
            new TestHostEnvironment(Environments.Production),
            "AxonVoiceAI.Gateway");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("DATA_PROTECTION_KEYS_DIRECTORY is required outside Development. Configure a persistent directory or Docker volume for ASP.NET Core DataProtection keys.");
    }

    [Fact]
    public void Resolve_ProductionWithoutCertificate_Throws()
    {
        var configuration = BuildConfiguration(("DATA_PROTECTION_KEYS_DIRECTORY", "/var/axonvoice/dataprotection"));

        var action = () => PlatformDataProtectionSettings.Resolve(
            configuration,
            new TestHostEnvironment(Environments.Production),
            "AxonVoiceAI.Gateway");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("DATA_PROTECTION_CERTIFICATE_BASE64 is required outside Development. Provide a base64-encoded password-protected PFX for DataProtection key encryption.");
    }

    [Fact]
    public void Resolve_CertificateWithoutPassword_Throws()
    {
        var configuration = BuildConfiguration(
            ("DATA_PROTECTION_KEYS_DIRECTORY", "/var/axonvoice/dataprotection"),
            ("DATA_PROTECTION_CERTIFICATE_BASE64", "ZmFrZQ=="));

        var action = () => PlatformDataProtectionSettings.Resolve(
            configuration,
            new TestHostEnvironment(Environments.Development),
            "AxonVoiceAI.Gateway");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("DATA_PROTECTION_CERTIFICATE_PASSWORD is required when DATA_PROTECTION_CERTIFICATE_BASE64 is set.");
    }

    [Fact]
    public void Resolve_ProductionWithCompleteConfiguration_ReturnsSettings()
    {
        var configuration = BuildConfiguration(
            ("DATA_PROTECTION_KEYS_DIRECTORY", "/var/axonvoice/dataprotection"),
            ("DATA_PROTECTION_CERTIFICATE_BASE64", "ZmFrZQ=="),
            ("DATA_PROTECTION_CERTIFICATE_PASSWORD", "change_me"));

        var settings = PlatformDataProtectionSettings.Resolve(
            configuration,
            new TestHostEnvironment(Environments.Production),
            "AxonVoiceAI.Gateway");

        settings.KeysDirectoryPath.Should().Be("/var/axonvoice/dataprotection");
        settings.CertificateBase64.Should().Be("ZmFrZQ==");
        settings.CertificatePassword.Should().Be("change_me");
        settings.GetServiceKeysDirectoryPath().Should().Be(Path.Combine("/var/axonvoice/dataprotection", "AxonVoiceAI.Gateway"));
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
