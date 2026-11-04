namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.GooglePubSub;

[TestFixture]
class EnvironmentDataTests
{
    [Test]
    public void Should_report_no_name_prefix_by_default() =>
        Assert.That(Read("ProjectId=contoso-project")["Transport.GooglePubSub.NamePrefix"], Is.EqualTo("None"));

    [Test]
    public void Should_report_a_configured_name_prefix_without_its_value()
    {
        var data = Read("ProjectId=contoso-project;ResourceNamePrefix=contoso-;ServiceEndpoint=contoso.example.com:443");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.GooglePubSub.NamePrefix"], Is.EqualTo("Configured"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    static Dictionary<string, string> Read(string connectionString) =>
        new GooglePubSubTransportCustomization()
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}