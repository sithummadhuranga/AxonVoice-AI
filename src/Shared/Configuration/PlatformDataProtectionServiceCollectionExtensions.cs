using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AxonVoiceAI.Shared.Configuration;

public static class PlatformDataProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string applicationName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var settings = PlatformDataProtectionSettings.Resolve(configuration, environment, applicationName);

        var builder = services
            .AddDataProtection()
            .SetApplicationName(settings.ApplicationName);

        if (settings.KeysDirectoryPath is null)
            return services;

        var keysDirectory = Directory.CreateDirectory(settings.GetServiceKeysDirectoryPath());
        builder.PersistKeysToFileSystem(keysDirectory);

        if (settings.CertificateBase64 is not null)
        {
            builder.ProtectKeysWithCertificate(LoadCertificate(settings.CertificateBase64, settings.CertificatePassword!));
        }

        return services;
    }

    private static X509Certificate2 LoadCertificate(string certificateBase64, string certificatePassword)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(certificateBase64),
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet,
                Pkcs12LoaderLimits.Defaults);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "DATA_PROTECTION_CERTIFICATE_BASE64 must contain a valid base64-encoded password-protected PFX.",
                ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Failed to load the DataProtection certificate. Verify DATA_PROTECTION_CERTIFICATE_BASE64 and DATA_PROTECTION_CERTIFICATE_PASSWORD.",
                ex);
        }
    }
}

internal sealed record PlatformDataProtectionSettings(
    string ApplicationName,
    string? KeysDirectoryPath,
    string? CertificateBase64,
    string? CertificatePassword)
{
    private const string KeysDirectoryKey = "DATA_PROTECTION_KEYS_DIRECTORY";
    private const string CertificateBase64Key = "DATA_PROTECTION_CERTIFICATE_BASE64";
    private const string CertificatePasswordKey = "DATA_PROTECTION_CERTIFICATE_PASSWORD";

    public string GetServiceKeysDirectoryPath()
    {
        if (KeysDirectoryPath is null)
            throw new InvalidOperationException("A DataProtection keys directory has not been configured.");

        return Path.Combine(KeysDirectoryPath, ApplicationName);
    }

    public static PlatformDataProtectionSettings Resolve(
        IConfiguration configuration,
        IHostEnvironment environment,
        string applicationName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var keysDirectoryPath = Normalize(configuration[KeysDirectoryKey]);
        var certificateBase64 = Normalize(configuration[CertificateBase64Key]);
        var certificatePassword = configuration[CertificatePasswordKey];

        if (certificateBase64 is not null && string.IsNullOrEmpty(certificatePassword))
        {
            throw new InvalidOperationException(
                $"{CertificatePasswordKey} is required when {CertificateBase64Key} is set.");
        }

        if (environment.IsDevelopment() && keysDirectoryPath is null)
        {
            return new PlatformDataProtectionSettings(applicationName, null, null, null);
        }

        if (keysDirectoryPath is null)
        {
            throw new InvalidOperationException(
                $"{KeysDirectoryKey} is required outside Development. Configure a persistent directory or Docker volume for ASP.NET Core DataProtection keys.");
        }

        if (!environment.IsDevelopment())
        {
            if (certificateBase64 is null)
            {
                throw new InvalidOperationException(
                    $"{CertificateBase64Key} is required outside Development. Provide a base64-encoded password-protected PFX for DataProtection key encryption.");
            }

            if (string.IsNullOrEmpty(certificatePassword))
            {
                throw new InvalidOperationException(
                    $"{CertificatePasswordKey} is required outside Development. Provide the password for the DataProtection PFX.");
            }
        }

        return new PlatformDataProtectionSettings(
            applicationName,
            keysDirectoryPath,
            certificateBase64,
            certificatePassword);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}