namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class HttpsSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<HttpsSettings>();

    public HttpsSettings(SettingsRootNamespace rootNamespace)
    {
        // Kestrel HTTPS - disabled by default for backwards compatibility
        Enabled = SettingsReader.Read(rootNamespace, "Https.Enabled", false);

        if (Enabled)
        {
            CertificatePath = SettingsReader.Read<string>(rootNamespace, "Https.CertificatePath");
            CertificatePassword = SettingsReader.Read<string>(rootNamespace, "Https.CertificatePassword");

            ValidateCertificateConfiguration();
        }

        // HTTPS redirection - disabled by default for backwards compatibility
        RedirectHttpToHttps = SettingsReader.Read(rootNamespace, "Https.RedirectHttpToHttps", false);

        // HSTS - disabled by default, only applies in non-development environments
        EnableHsts = SettingsReader.Read(rootNamespace, "Https.EnableHsts", false);
        HstsMaxAgeSeconds = SettingsReader.Read(rootNamespace, "Https.HstsMaxAgeSeconds", 31536000); // 1 year default
        HstsIncludeSubDomains = SettingsReader.Read(rootNamespace, "Https.HstsIncludeSubDomains", false);

        LogConfiguration();
    }

    /// <summary>
    /// When true, Kestrel will be configured to listen on HTTPS using the specified certificate.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Path to the HTTPS certificate file (.pfx or .pem).
    /// Required when Https.Enabled is true.
    /// </summary>
    public string CertificatePath { get; }

    /// <summary>
    /// Password for the HTTPS certificate.
    /// Can be null for certificates without a password.
    /// </summary>
    public string CertificatePassword { get; }

    public bool RedirectHttpToHttps { get; }

    public bool EnableHsts { get; }

    public int HstsMaxAgeSeconds { get; }

    public bool HstsIncludeSubDomains { get; }

    void ValidateCertificateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            throw new InvalidOperationException(
                "Https.Enabled is true but Https.CertificatePath is not configured. " +
                "Please specify the path to a valid HTTPS certificate file (.pfx or .pem).");
        }

        if (!File.Exists(CertificatePath))
        {
            throw new InvalidOperationException(
                $"Https.CertificatePath '{CertificatePath}' does not exist. " +
                "Please specify a valid path to an HTTPS certificate file.");
        }
    }

    void LogConfiguration()
    {
        logger.LogInformation("HTTPS configuration:");

        logger.LogInformation("  Enabled: {Enabled}", Enabled);
        logger.LogInformation("  CertificatePath: {CertificatePath}", CertificatePath);
        logger.LogInformation("  CertificatePassword: {CertificatePassword}", string.IsNullOrEmpty(CertificatePassword) ? "(not set)" : "(set)");
        logger.LogInformation("  RedirectHttpToHttps: {RedirectHttpToHttps}", RedirectHttpToHttps);
        logger.LogInformation("  EnableHsts: {EnableHsts}", EnableHsts);
        logger.LogInformation("  HstsMaxAgeSeconds: {HstsMaxAgeSeconds}", HstsMaxAgeSeconds);
        logger.LogInformation("  HstsIncludeSubDomains: {HstsIncludeSubDomains}", HstsIncludeSubDomains);
    }
}
