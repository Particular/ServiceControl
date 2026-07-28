namespace ServiceControl.UnitTests.Operations;

using System;
using System.Collections.Generic;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
public class When_deriving_a_failed_error_import_key
{
    [Test]
    public void Uses_the_unique_message_id_when_headers_are_well_formed()
    {
        var headers = new Dictionary<string, string>
        {
            { Headers.MessageId, "message-1" },
            { Headers.ProcessingEndpoint, "Sales" }
        };

        var key = FailedErrorImport.DeriveKey(headers, "native-1");

        Assert.That(key, Is.EqualTo(DeterministicGuid.MakeId("message-1", "Sales")));
    }

    [Test]
    public void Prefers_an_existing_retry_unique_message_id()
    {
        var uniqueMessageId = Guid.NewGuid();
        var headers = new Dictionary<string, string>
        {
            { "ServiceControl.Retry.UniqueMessageId", uniqueMessageId.ToString() }
        };

        var key = FailedErrorImport.DeriveKey(headers, "native-1");

        Assert.That(key, Is.EqualTo(uniqueMessageId));
    }

    [Test]
    public void Falls_back_to_the_native_id_when_no_processing_endpoint_can_be_derived()
    {
        var headers = new Dictionary<string, string>
        {
            { Headers.MessageId, "message-1" }
        };

        var key = FailedErrorImport.DeriveKey(headers, "native-1");

        Assert.That(key, Is.EqualTo(DeterministicGuid.MakeId("native-1")));
    }

    [Test]
    public void Falls_back_when_the_retry_unique_message_id_is_not_a_guid()
    {
        var headers = new Dictionary<string, string>
        {
            { "ServiceControl.Retry.UniqueMessageId", "not-a-guid" }
        };

        var key = FailedErrorImport.DeriveKey(headers, "native-1");

        Assert.That(key, Is.EqualTo(DeterministicGuid.MakeId("native-1")));
    }

    [Test]
    public void Is_stable_across_repeated_failures_of_the_same_malformed_message()
    {
        var first = FailedErrorImport.DeriveKey(new Dictionary<string, string>(), "native-1");
        var second = FailedErrorImport.DeriveKey(new Dictionary<string, string>(), "native-1");

        Assert.That(second, Is.EqualTo(first));
    }
}
