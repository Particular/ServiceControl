namespace ServiceControl.Infrastructure;

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ServiceControl.Configuration;

public class HttpsSettings
{
    readonly ILogger logger = LoggerUtil.CreateStaticLogger<HttpsSettings>();

    /// <summary>
    /// Initializes HTTPS settings from the given configuration root namespace.
    /// </summary>
    /// <param name="rootNamespace"></param>
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
        HttpsPort = SettingsReader.Read<int?>(rootNamespace, "Https.Port", null);

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

    /// <summary>
    /// When true, HTTP requests will be redirected to HTTPS.
    /// Requires HTTPS to be properly configured. Default is false.
    /// </summary>
    public bool RedirectHttpToHttps { get; }

    /// <summary>
    /// The port to redirect HTTPS requests to. If not specified, uses the default HTTPS port (443).
    /// Only used when RedirectHttpToHttps is true.
    /// </summary>
    public int? HttpsPort { get; }

    /// <summary>
    /// When true, enables HTTP Strict Transport Security (HSTS) headers.
    /// HSTS instructs browsers to only access the site via HTTPS. Default is false.
    /// Only applies in non-development environments.
    /// </summary>
    public bool EnableHsts { get; }

    /// <summary>
    /// The max-age value for the HSTS header in seconds. Default is 31536000 (1 year).
    /// Only used when EnableHsts is true.
    /// </summary>
    public int HstsMaxAgeSeconds { get; }

    /// <summary>
    /// When true, includes subdomains in the HSTS policy. Default is false.
    /// Only used when EnableHsts is true.
    /// </summary>
    public bool HstsIncludeSubDomains { get; }

    void ValidateCertificateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            var message = "Https.CertificatePath is required when HTTPS is enabled. Please specify the path to a valid HTTPS certificate file (.pfx or .pem)";
            logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        if (!File.Exists(CertificatePath))
        {
            var message = $"Https.CertificatePath does not exist. Current value: '{CertificatePath}'";
            logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }
    }

    void LogConfiguration()
    {
        var httpsPortDisplay = HttpsPort.HasValue ? HttpsPort.Value.ToString() : "(default)";

        if (!Enabled)
        {
            logger.LogInformation("HTTPS is disabled: Enabled={Enabled}, RedirectHttpToHttps={RedirectHttpToHttps}, HttpsPort={HttpsPort}, EnableHsts={EnableHsts}, HstsMaxAgeSeconds={HstsMaxAgeSeconds}, HstsIncludeSubDomains={HstsIncludeSubDomains}",
                Enabled, RedirectHttpToHttps, httpsPortDisplay, EnableHsts, HstsMaxAgeSeconds, HstsIncludeSubDomains);
        }
        else
        {
            logger.LogInformation("HTTPS is enabled: Enabled={Enabled}, CertificatePath={CertificatePath}, HasCertificatePassword={HasCertificatePassword}, RedirectHttpToHttps={RedirectHttpToHttps}, HttpsPort={HttpsPort}, EnableHsts={EnableHsts}, HstsMaxAgeSeconds={HstsMaxAgeSeconds}, HstsIncludeSubDomains={HstsIncludeSubDomains}",
                Enabled, CertificatePath, !string.IsNullOrEmpty(CertificatePassword), RedirectHttpToHttps, httpsPortDisplay, EnableHsts, HstsMaxAgeSeconds, HstsIncludeSubDomains);
        }

        // Warn about potential misconfigurations
        if (RedirectHttpToHttps && !EnableHsts)
        {
            logger.LogWarning("HTTPS redirect is enabled but HSTS is disabled. Consider enabling Https.EnableHsts for better security");
        }

        if (!Enabled && EnableHsts)
        {
            logger.LogWarning("HSTS is enabled but Kestrel HTTPS is disabled. HSTS headers will only be effective if TLS is terminated by a reverse proxy");
        }

        if (!Enabled && RedirectHttpToHttps)
        {
            logger.LogWarning("HTTPS redirect is enabled but Kestrel HTTPS is disabled. Redirect will only work if TLS is terminated by a reverse proxy");
        }

        if (HttpsPort.HasValue && !RedirectHttpToHttps)
        {
            logger.LogWarning("Https.Port is configured but HTTPS redirect is disabled. The port setting will be ignored");
        }

        if (RedirectHttpToHttps && !HttpsPort.HasValue)
        {
            logger.LogInformation("Https.Port is not configured. HTTPS redirect will be ignored.");
        }
    }
}
