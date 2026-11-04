namespace ServiceControl.Transport.Tests;

using System;
using NUnit.Framework;
using Transports.GooglePubSub;

[TestFixture]
class ConnectionStringTests
{
    [Test]
    public void Should_accept_an_empty_connection_string()
    {
        var connectionString = new GooglePubSubConnectionString(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(connectionString.ProjectId, Is.Null);
            Assert.That(connectionString.ResourceNamePrefix, Is.Null);
            Assert.That(connectionString.ServiceEndpoint, Is.Null);
        }
    }

    [Test]
    public void Should_read_every_supported_key_case_insensitively()
    {
        var connectionString = new GooglePubSubConnectionString("projectid=contoso-project;ResourceNamePrefix=dev-;SERVICEENDPOINT=europe-west1-pubsub.googleapis.com:443");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(connectionString.ProjectId, Is.EqualTo("contoso-project"));
            Assert.That(connectionString.ResourceNamePrefix, Is.EqualTo("dev-"));
            Assert.That(connectionString.ServiceEndpoint, Is.EqualTo("europe-west1-pubsub.googleapis.com:443"));
        }
    }

    [Test]
    public void Should_reject_unsupported_keys_without_echoing_their_values()
    {
        var exception = Assert.Throws<ArgumentException>(() => _ = new GooglePubSubConnectionString("ProjectId=contoso-project;CredentialsJson=contoso-secret"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("CredentialsJson").IgnoreCase);
            Assert.That(exception.Message, Does.Not.Contain("contoso-secret"));
        }
    }
}