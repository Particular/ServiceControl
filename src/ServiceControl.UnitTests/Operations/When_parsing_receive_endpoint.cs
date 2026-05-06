namespace ServiceControl.UnitTests.Operations;

using System.Collections.Generic;
using NServiceBus.Faults;
using NUnit.Framework;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure;

[TestFixture]
public class When_parsing_receive_endpoint
{
    [Test]
    public void Should_infer_host_from_machine_name_in_failed_queue_when_host_header_is_missing()
    {
        var headers = new Dictionary<string, string>
        {
            { FaultsHeaderKeys.FailedQ, "Sales@backend-01" }
        };

        var endpoint = EndpointDetailsParser.ReceivingEndpoint(headers);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint, Is.Not.Null);
            Assert.That(endpoint.Name, Is.EqualTo("Sales"));
            Assert.That(endpoint.Host, Is.EqualTo("backend-01"));
            Assert.That(endpoint.HostId, Is.EqualTo(DeterministicGuid.MakeId("Sales", "backend-01")));
        });
    }

    [Test]
    public void Should_fallback_to_unknown_if_host_can_not_be_determined()
    {
        var headers = new Dictionary<string, string>
        {
            { FaultsHeaderKeys.FailedQ, "Billing" }
        };

        var endpoint = EndpointDetailsParser.ReceivingEndpoint(headers);

        Assert.Multiple(() =>
        {
            Assert.That(endpoint, Is.Not.Null);
            Assert.That(endpoint.Name, Is.EqualTo("Billing"));
            Assert.That(endpoint.Host, Is.EqualTo("unknown"));
            Assert.That(endpoint.HostId, Is.EqualTo(DeterministicGuid.MakeId("Billing", "unknown")));
        });
    }
}