namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using System.IO;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

/// <summary>
/// Tests for <see cref="HttpsSettings"/> which is shared infrastructure code
/// used by all three instance types (ServiceControl, ServiceControl.Audit, ServiceControl.Monitoring).
/// Each instance passes a different <see cref="SettingsRootNamespace"/> which only affects
/// the environment variable prefix (e.g., SERVICECONTROL_, SERVICECONTROL_AUDIT_, MONITORING_).
/// The parsing logic is identical, so testing with one namespace is sufficient.
/// </summary>
[TestFixture]
public class HttpsSettingsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    string tempCertPath;

    [SetUp]
    public void SetUp() =>
        // Create a temporary file to simulate a certificate file
        tempCertPath = Path.GetTempFileName();

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPATH", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_PORT", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLEHSTS", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS", null);

        // Clean up temp file
        if (File.Exists(tempCertPath))
        {
            File.Delete(tempCertPath);
        }
    }

    [Test]
    public void Should_default_to_disabled()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.Enabled, Is.False);
    }

    [Test]
    public void Should_default_redirect_to_disabled()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.RedirectHttpToHttps, Is.False);
    }

    [Test]
    public void Should_default_hsts_to_disabled()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.EnableHsts, Is.False);
    }

    [Test]
    public void Should_default_hsts_max_age_to_one_year()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HstsMaxAgeSeconds, Is.EqualTo(31536000));
    }

    [Test]
    public void Should_default_hsts_include_subdomains_to_false()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HstsIncludeSubDomains, Is.False);
    }

    [Test]
    public void Should_default_https_port_to_null()
    {
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HttpsPort, Is.Null);
    }

    [Test]
    public void Should_enable_https_when_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPATH", tempCertPath);

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.Enabled, Is.True);
    }

    [Test]
    public void Should_read_certificate_path()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPATH", tempCertPath);

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.CertificatePath, Is.EqualTo(tempCertPath));
    }

    [Test]
    public void Should_read_certificate_password()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPATH", tempCertPath);
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPASSWORD", "my-secret-password");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.CertificatePassword, Is.EqualTo("my-secret-password"));
    }

    [Test]
    public void Should_throw_when_https_enabled_without_certificate_path()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", "true");

        var ex = Assert.Throws<InvalidOperationException>(() => new HttpsSettings(TestNamespace));

        Assert.That(ex.Message, Does.Contain("CertificatePath is required"));
    }

    [Test]
    public void Should_throw_when_certificate_path_does_not_exist()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_CERTIFICATEPATH", "/nonexistent/path/cert.pfx");

        var ex = Assert.Throws<InvalidOperationException>(() => new HttpsSettings(TestNamespace));

        Assert.That(ex.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void Should_enable_redirect_when_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS", "true");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.RedirectHttpToHttps, Is.True);
    }

    [Test]
    public void Should_read_https_port()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_PORT", "8443");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HttpsPort, Is.EqualTo(8443));
    }

    [Test]
    public void Should_enable_hsts_when_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLEHSTS", "true");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.EnableHsts, Is.True);
    }

    [Test]
    public void Should_read_custom_hsts_max_age()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS", "86400");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HstsMaxAgeSeconds, Is.EqualTo(86400));
    }

    [Test]
    public void Should_enable_hsts_include_subdomains_when_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS", "true");

        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.HstsIncludeSubDomains, Is.True);
    }

    [Test]
    public void Should_not_validate_certificate_when_disabled()
    {
        // HTTPS disabled (default) should not require certificate
        var settings = new HttpsSettings(TestNamespace);

        Assert.That(settings.Enabled, Is.False);
        Assert.That(settings.CertificatePath, Is.Null);
    }

    [Test]
    public void Should_allow_redirect_settings_without_https_enabled()
    {
        // Redirect settings can be configured even when HTTPS is disabled
        // (they will be ignored but should not throw)
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_REDIRECTHTTPTOHTTPS", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_PORT", "443");

        var settings = new HttpsSettings(TestNamespace);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.RedirectHttpToHttps, Is.True);
            Assert.That(settings.HttpsPort, Is.EqualTo(443));
        }
    }

    [Test]
    public void Should_allow_hsts_settings_without_https_enabled()
    {
        // HSTS settings can be configured even when HTTPS is disabled
        // (they will be ignored but should not throw)
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_ENABLEHSTS", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSMAXAGESECONDS", "3600");
        Environment.SetEnvironmentVariable("SERVICECONTROL_HTTPS_HSTSINCLUDESUBDOMAINS", "true");

        var settings = new HttpsSettings(TestNamespace);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.EnableHsts, Is.True);
            Assert.That(settings.HstsMaxAgeSeconds, Is.EqualTo(3600));
            Assert.That(settings.HstsIncludeSubDomains, Is.True);
        }
    }
}
