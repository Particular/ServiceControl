namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

/// <summary>
/// Tests for <see cref="CorsSettings"/> which is shared infrastructure code
/// used by all three instance types (ServiceControl, ServiceControl.Audit, ServiceControl.Monitoring).
/// Each instance passes a different <see cref="SettingsRootNamespace"/> which only affects
/// the environment variable prefix (e.g., SERVICECONTROL_, SERVICECONTROL_AUDIT_, MONITORING_).
/// The parsing logic is identical, so testing with one namespace is sufficient.
/// </summary>
[TestFixture]
public class CorsSettingsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWANYORIGIN", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", null);
    }

    [Test]
    public void Should_default_to_allow_any_origin()
    {
        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowAnyOrigin, Is.True);
    }

    [Test]
    public void Should_default_to_empty_allowed_origins()
    {
        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Is.Empty);
    }

    [Test]
    public void Should_parse_single_allowed_origin()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(1));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
    }

    [Test]
    public void Should_parse_multiple_comma_separated_origins()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com,https://app.example.com,http://localhost:3000");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(3));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://app.example.com"));
        Assert.That(settings.AllowedOrigins, Does.Contain("http://localhost:3000"));
    }

    [Test]
    public void Should_parse_semicolon_separated_origins()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com;https://other.com");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(2));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://other.com"));
    }

    [Test]
    public void Should_trim_whitespace_from_origins()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", " https://example.com , https://other.com ");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(2));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://other.com"));
    }

    [Test]
    public void Should_normalize_origin_to_scheme_and_authority()
    {
        // Origin with path should be normalized to just scheme://authority
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com/some/path");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(1));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
    }

    [Test]
    public void Should_preserve_port_in_origin()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "http://localhost:8080");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(1));
        Assert.That(settings.AllowedOrigins, Does.Contain("http://localhost:8080"));
    }

    [Test]
    public void Should_ignore_invalid_origins()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com,not-a-url,https://other.com");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Has.Count.EqualTo(2));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://example.com"));
        Assert.That(settings.AllowedOrigins, Does.Contain("https://other.com"));
        Assert.That(settings.AllowedOrigins, Does.Not.Contain("not-a-url"));
    }

    [Test]
    public void Should_disable_allow_any_origin_when_specific_origins_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "https://example.com");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowAnyOrigin, Is.False);
    }

    [Test]
    public void Should_respect_explicit_allow_any_origin_false()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWANYORIGIN", "false");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowAnyOrigin, Is.False);
    }

    [Test]
    public void Should_keep_allow_any_origin_true_when_explicitly_set()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWANYORIGIN", "true");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowAnyOrigin, Is.True);
    }

    [Test]
    public void Should_handle_empty_origins_string()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Is.Empty);
        Assert.That(settings.AllowAnyOrigin, Is.True);
    }

    [Test]
    public void Should_handle_whitespace_only_origins_string()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "   ");

        var settings = new CorsSettings(TestNamespace);

        Assert.That(settings.AllowedOrigins, Is.Empty);
        Assert.That(settings.AllowAnyOrigin, Is.True);
    }

    [Test]
    public void Should_handle_only_invalid_origins()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_CORS_ALLOWEDORIGINS", "not-a-url,also-not-valid");

        var settings = new CorsSettings(TestNamespace);

        // All origins invalid, so list is empty, but AllowAnyOrigin stays true (no valid origins to override)
        Assert.That(settings.AllowedOrigins, Is.Empty);
        Assert.That(settings.AllowAnyOrigin, Is.True);
    }
}
