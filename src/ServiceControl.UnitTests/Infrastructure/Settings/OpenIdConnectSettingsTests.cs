namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

/// <summary>
/// Tests for <see cref="OpenIdConnectSettings"/> which is shared infrastructure code
/// used by all three instance types (ServiceControl, ServiceControl.Audit, ServiceControl.Monitoring).
/// Each instance passes a different <see cref="SettingsRootNamespace"/> which only affects
/// the environment variable prefix (e.g., SERVICECONTROL_, SERVICECONTROL_AUDIT_, MONITORING_).
/// The parsing logic is identical, so testing with one namespace is sufficient.
/// </summary>
[TestFixture]
public class OpenIdConnectSettingsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATELIFETIME", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEISSUERSIGNINGKEY", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY", null);
    }

    [Test]
    public void Should_default_to_disabled()
    {
        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        Assert.That(settings.Enabled, Is.False);
    }

    [Test]
    public void Should_default_validation_flags_to_true()
    {
        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ValidateIssuer, Is.True);
            Assert.That(settings.ValidateAudience, Is.True);
            Assert.That(settings.ValidateLifetime, Is.True);
            Assert.That(settings.ValidateIssuerSigningKey, Is.True);
            Assert.That(settings.RequireHttpsMetadata, Is.True);
        }
    }

    [Test]
    public void Should_read_authority()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        Assert.That(settings.Authority, Is.EqualTo("https://login.example.com"));
    }

    [Test]
    public void Should_read_audience()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-api-audience");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        Assert.That(settings.Audience, Is.EqualTo("my-api-audience"));
    }

    [Test]
    public void Should_read_validation_flags()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEISSUER", "false");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEAUDIENCE", "false");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATELIFETIME", "false");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_VALIDATEISSUERSIGNINGKEY", "false");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA", "false");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ValidateIssuer, Is.False);
            Assert.That(settings.ValidateAudience, Is.False);
            Assert.That(settings.ValidateLifetime, Is.False);
            Assert.That(settings.ValidateIssuerSigningKey, Is.False);
            Assert.That(settings.RequireHttpsMetadata, Is.False);
        }
    }

    [Test]
    public void Should_read_service_pulse_settings_when_required()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", "api://my-api/.default");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY", "https://pulse-auth.example.com");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false, requireServicePulseSettings: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ServicePulseClientId, Is.EqualTo("my-client-id"));
            Assert.That(settings.ServicePulseApiScopes, Is.EqualTo("api://my-api/.default"));
            Assert.That(settings.ServicePulseAuthority, Is.EqualTo("https://pulse-auth.example.com"));
        }
    }

    [Test]
    public void Should_not_read_service_pulse_settings_when_not_required()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", "api://my-api/.default");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false, requireServicePulseSettings: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ServicePulseClientId, Is.Null);
            Assert.That(settings.ServicePulseApiScopes, Is.Null);
        }
    }

    [Test]
    public void Should_throw_when_enabled_without_authority()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false));

        Assert.That(ex.Message, Does.Contain("Authority is required"));
    }

    [Test]
    public void Should_throw_when_enabled_without_audience()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false));

        Assert.That(ex.Message, Does.Contain("Audience is required"));
    }

    [Test]
    public void Should_throw_when_authority_is_not_valid_uri()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "not-a-valid-uri");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false));

        Assert.That(ex.Message, Does.Contain("must be a valid absolute URI"));
    }

    [Test]
    public void Should_throw_when_authority_is_http_and_https_required()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "http://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false));

        Assert.That(ex.Message, Does.Contain("must use HTTPS"));
    }

    [Test]
    public void Should_allow_http_authority_when_https_not_required()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "http://localhost:5000");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_REQUIREHTTPSMETADATA", "false");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false);

        Assert.That(settings.Authority, Is.EqualTo("http://localhost:5000"));
    }

    [Test]
    public void Should_throw_when_service_pulse_client_id_missing()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true));

        Assert.That(ex.Message, Does.Contain("ServicePulse.ClientId is required"));
    }

    [Test]
    public void Should_throw_when_service_pulse_api_scopes_missing()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true));

        Assert.That(ex.Message, Does.Contain("ServicePulse.ApiScopes is required"));
    }

    [Test]
    public void Should_throw_when_service_pulse_authority_is_invalid()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", "api://my-api/.default");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY", "not-a-valid-uri");

        var ex = Assert.Throws<Exception>(() => new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true));

        Assert.That(ex.Message, Does.Contain("ServicePulse.Authority must be a valid absolute URI"));
    }

    [Test]
    public void Should_succeed_with_valid_full_configuration()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", "api://my-api/.default");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.Authority, Is.EqualTo("https://login.example.com"));
            Assert.That(settings.Audience, Is.EqualTo("my-audience"));
            Assert.That(settings.ServicePulseClientId, Is.EqualTo("my-client-id"));
            Assert.That(settings.ServicePulseApiScopes, Is.EqualTo("api://my-api/.default"));
        }
    }

    [Test]
    public void Should_succeed_without_service_pulse_settings_when_not_required()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.Authority, Is.EqualTo("https://login.example.com"));
            Assert.That(settings.Audience, Is.EqualTo("my-audience"));
        }
    }

    [Test]
    public void Should_not_validate_when_disabled()
    {
        // Even without required settings, validation should pass when auth is disabled
        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true);

        Assert.That(settings.Enabled, Is.False);
    }

    [Test]
    public void Should_allow_optional_service_pulse_authority()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUTHORITY", "https://login.example.com");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_AUDIENCE", "my-audience");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_CLIENTID", "my-client-id");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_APISCOPES", "api://my-api/.default");
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_SERVICEPULSE_AUTHORITY", "https://pulse-auth.example.com");

        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: true, requireServicePulseSettings: true);

        Assert.That(settings.ServicePulseAuthority, Is.EqualTo("https://pulse-auth.example.com"));
    }
}
