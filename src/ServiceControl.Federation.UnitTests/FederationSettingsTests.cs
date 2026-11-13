namespace ServiceControl.Federation.UnitTests;

using System;
using System.Linq;
using Configuration;
using NUnit.Framework;

[TestFixture]
class FederationSettingsTests
{
    [Test]
    public void Instances_are_parsed_in_the_remote_instances_style()
    {
        var members = FederationSettings.ParseMembers("""
            [
              { "api_uri": "http://sc-eu:33333/api/", "monitoring_uri": "http://mon-eu:33633/", "key": "eu", "name": "Europe" },
              { "api_uri": "http://sc-us:33333/api/" }
            ]
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(members.Select(m => m.Key), Is.EqualTo(new[] { "eu", "sc-us-33333" }), "a missing key is derived from the api_uri host and port");
            Assert.That(members[0].Name, Is.EqualTo("Europe"));
            Assert.That(members[0].MonitoringUrl, Is.EqualTo("http://mon-eu:33633/"));
            Assert.That(members[1].MonitoringUrl, Is.Null);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("[]")]
    public void No_instances_fail_validation(string? json)
    {
        var settings = new FederationSettings { Members = [.. FederationSettings.ParseMembers(json)] };

        Assert.That(settings.Validate, Throws.InvalidOperationException.With.Message.Contains("FederatedInstances"));
    }

    [Test]
    public void Instances_that_derive_the_same_key_must_be_given_distinct_keys()
    {
        var settings = new FederationSettings
        {
            Members = [.. FederationSettings.ParseMembers("""[{ "api_uri": "http://localhost:33333/api/" }, { "api_uri": "http://localhost:33333/api" }]""")]
        };

        Assert.That(settings.Validate, Throws.InvalidOperationException.With.Message.Contains("distinct"));
    }

    [Test]
    public void Invalid_urls_fail_validation()
    {
        var settings = new FederationSettings { Members = [new MemberSettings { Key = "a", PrimaryUrl = "not a url" }] };

        Assert.That(settings.Validate, Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("api_uri"));
    }
}
