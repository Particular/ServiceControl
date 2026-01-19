namespace ServiceControl.UnitTests.Infrastructure.Settings;

using System;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

/// <summary>
/// Tests for <see cref="ForwardedHeadersSettings"/> which is shared infrastructure code
/// used by all three instance types (ServiceControl, ServiceControl.Audit, ServiceControl.Monitoring).
/// Each instance passes a different <see cref="SettingsRootNamespace"/> which only affects
/// the environment variable prefix (e.g., SERVICECONTROL_, SERVICECONTROL_AUDIT_, MONITORING_).
/// The parsing logic is identical, so testing with one namespace is sufficient.
/// </summary>
[TestFixture]
public class ForwardedHeadersSettingsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_ENABLED", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_TRUSTALLPROXIES", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS", null);
    }

    [Test]
    public void Should_parse_known_proxies_from_comma_separated_list()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1,10.0.0.5,192.168.1.1");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownProxiesRaw, Has.Count.EqualTo(3));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("127.0.0.1"));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("10.0.0.5"));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("192.168.1.1"));
    }

    [Test]
    public void Should_parse_known_proxies_to_ip_addresses()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1,10.0.0.5");

        var settings = new ForwardedHeadersSettings(TestNamespace);
        var ipAddresses = settings.KnownProxies.ToList();

        Assert.That(ipAddresses, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ipAddresses[0].ToString(), Is.EqualTo("127.0.0.1"));
            Assert.That(ipAddresses[1].ToString(), Is.EqualTo("10.0.0.5"));
        }
    }

    [Test]
    public void Should_ignore_invalid_ip_addresses()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1,not-an-ip,10.0.0.5");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownProxiesRaw, Has.Count.EqualTo(2));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("127.0.0.1"));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("10.0.0.5"));
        Assert.That(settings.KnownProxiesRaw, Does.Not.Contain("not-an-ip"));
    }

    [Test]
    public void Should_parse_known_networks_from_comma_separated_cidr()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS", "10.0.0.0/8,172.16.0.0/12,192.168.0.0/16");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownNetworks, Has.Count.EqualTo(3));
        Assert.That(settings.KnownNetworks, Does.Contain("10.0.0.0/8"));
        Assert.That(settings.KnownNetworks, Does.Contain("172.16.0.0/12"));
        Assert.That(settings.KnownNetworks, Does.Contain("192.168.0.0/16"));
    }

    [Test]
    public void Should_ignore_invalid_network_cidr()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS", "10.0.0.0/8,invalid-network,172.16.0.0/12");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownNetworks, Has.Count.EqualTo(2));
        Assert.That(settings.KnownNetworks, Does.Contain("10.0.0.0/8"));
        Assert.That(settings.KnownNetworks, Does.Contain("172.16.0.0/12"));
        Assert.That(settings.KnownNetworks, Does.Not.Contain("invalid-network"));
    }

    [Test]
    public void Should_disable_trust_all_proxies_when_known_proxies_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.TrustAllProxies, Is.False);
    }

    [Test]
    public void Should_disable_trust_all_proxies_when_known_networks_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNNETWORKS", "10.0.0.0/8");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.TrustAllProxies, Is.False);
    }

    [Test]
    public void Should_default_to_enabled()
    {
        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.Enabled, Is.True);
    }

    [Test]
    public void Should_default_to_trust_all_proxies()
    {
        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.TrustAllProxies, Is.True);
    }

    [Test]
    public void Should_respect_explicit_disabled_setting()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_ENABLED", "false");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.Enabled, Is.False);
    }

    [Test]
    public void Should_handle_semicolon_separator_in_proxies()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1;10.0.0.5");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownProxiesRaw, Has.Count.EqualTo(2));
    }

    [Test]
    public void Should_trim_whitespace_from_proxy_entries()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", " 127.0.0.1 , 10.0.0.5 ");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownProxiesRaw, Has.Count.EqualTo(2));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("127.0.0.1"));
        Assert.That(settings.KnownProxiesRaw, Does.Contain("10.0.0.5"));
    }
}
