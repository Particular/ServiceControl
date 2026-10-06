namespace ServiceControl.Transport.Tests;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Transports;
using Transports.SQS;

[TestFixture]
class EnvironmentDataTests
{
    [Test]
    public void Should_report_defaults_when_the_connection_string_carries_only_a_region()
    {
        var data = Read("Region=eu-west-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.AmazonSQS.NamePrefixes"], Is.EqualTo("None"));
            Assert.That(data["Transport.AmazonSQS.LargeMessageBucket"], Is.EqualTo("None"));
            Assert.That(data["Transport.AmazonSQS.MessageWrapping"], Is.EqualTo("Enabled"));
            Assert.That(data["Transport.AmazonSQS.ReservedBytesInMessageSize"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public void Should_report_selected_options_without_their_values()
    {
        var data = Read("AccessKeyId=AKIACONTOSO;SecretAccessKey=contoso-secret;Region=eu-west-1;QueueNamePrefix=contoso-;S3BucketForLargeMessages=contoso-bodies;DoNotWrapOutgoingMessages=true;ReservedBytesInMessageSize=1024");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Transport.AmazonSQS.NamePrefixes"], Is.EqualTo("Queue"));
            Assert.That(data["Transport.AmazonSQS.LargeMessageBucket"], Is.EqualTo("Configured"));
            Assert.That(data["Transport.AmazonSQS.MessageWrapping"], Is.EqualTo("Disabled"));
            Assert.That(data["Transport.AmazonSQS.ReservedBytesInMessageSize"], Is.EqualTo("1024"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    [TestCase("QueueNamePrefix=a-;TopicNamePrefix=b-", "QueueAndTopic")]
    [TestCase("TopicNamePrefix=b-", "Topic")]
    public void Should_report_which_name_prefixes_are_set(string prefixes, string expected) =>
        Assert.That(Read($"Region=eu-west-1;{prefixes}")["Transport.AmazonSQS.NamePrefixes"], Is.EqualTo(expected));

    static Dictionary<string, string> Read(string connectionString) =>
        new SQSTransportCustomization()
            .GetEnvironmentData(new TransportSettings { ConnectionString = connectionString })
            .ToDictionary(datum => datum.Key, datum => datum.ReadValue());
}
