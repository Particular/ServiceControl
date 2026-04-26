namespace ServiceControl.UnitTests.Operations;

using System.Collections.Generic;
using NServiceBus.Faults;
using NUnit.Framework;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure;

[TestFixture]
public class EndpointDetailsParserTests
{
    [Test]
    public void Receiving_endpoint_should_use_failed_queue_machine_when_host_is_missing()
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
    public void Receiving_endpoint_should_use_unknown_host_when_failed_queue_is_used_to_infer_endpoint_name()
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