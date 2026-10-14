namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using NServiceBus;
using NServiceBus.Faults;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.RavenDB.DataMigration;
// Aliased because the RavenDB persister's own EndpointDetailsParser would otherwise win from this namespace.
using IngestionParser = Contracts.Operations.EndpointDetailsParser;

[TestFixture]
class MigrationEndpointDetailsParserParityTests
{
    [TestCaseSource(nameof(HeaderSets))]
    public void Rebuilds_both_endpoints_exactly_as_ingestion_does(Dictionary<string, string> headers)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Shape(MigrationEndpointDetailsParser.SendingEndpoint(headers)), Is.EqualTo(Shape(IngestionParser.SendingEndpoint(headers))),
                "a migrated message whose sending endpoint differs from a newly ingested one is grouped apart from it in ServicePulse");
            Assert.That(Shape(MigrationEndpointDetailsParser.ReceivingEndpoint(headers)), Is.EqualTo(Shape(IngestionParser.ReceivingEndpoint(headers))),
                "a migrated message whose receiving endpoint differs from a newly ingested one is grouped apart from it in ServicePulse");
        }
    }

    static (string Name, string Host, Guid HostId)? Shape(EndpointDetails endpoint) =>
        endpoint is null ? null : (endpoint.Name, endpoint.Host, endpoint.HostId);

    static IEnumerable<TestCaseData> HeaderSets()
    {
        var sendingHostId = Guid.Parse("6a3a9b1c-2d4e-4f50-8a61-7b8c9d0e1f20").ToString();
        var receivingHostId = Guid.Parse("0f1e2d3c-4b5a-4968-8776-655443322110").ToString();

        yield return Case("full headers", new()
        {
            [Headers.OriginatingEndpoint] = "Sales",
            [Headers.OriginatingMachine] = "SALES-01",
            [Headers.OriginatingHostId] = sendingHostId,
            [Headers.ProcessingEndpoint] = "Billing",
            [Headers.HostDisplayName] = "Billing host",
            [Headers.ProcessingMachine] = "BILLING-01",
            [Headers.HostId] = receivingHostId
        });

        yield return Case("non-GUID host ids", new()
        {
            [Headers.OriginatingEndpoint] = "Sales",
            [Headers.OriginatingMachine] = "SALES-01",
            [Headers.OriginatingHostId] = "not-a-guid",
            [Headers.ProcessingEndpoint] = "Billing",
            [Headers.ProcessingMachine] = "BILLING-01",
            [Headers.HostId] = "not-a-guid"
        });

        yield return Case("null-valued host ids", new()
        {
            [Headers.OriginatingEndpoint] = "Sales",
            [Headers.OriginatingMachine] = "SALES-01",
            [Headers.OriginatingHostId] = null,
            [Headers.ProcessingEndpoint] = "Billing",
            [Headers.ProcessingMachine] = "BILLING-01",
            [Headers.HostId] = null
        });

        yield return Case("null-valued host display name", new()
        {
            [Headers.ProcessingEndpoint] = "Billing",
            [Headers.HostDisplayName] = null,
            [Headers.ProcessingMachine] = "BILLING-01",
            [Headers.HostId] = receivingHostId
        });

        yield return Case("addresses with a machine", new()
        {
            [Headers.OriginatingAddress] = "Sales@SALES-01",
            [FaultsHeaderKeys.FailedQ] = "Billing@BILLING-01"
        });

        yield return Case("addresses without a machine", new()
        {
            [Headers.OriginatingAddress] = "Sales",
            [FaultsHeaderKeys.FailedQ] = "Billing"
        });

        yield return Case("failed queue with a non-GUID host id", new()
        {
            [FaultsHeaderKeys.FailedQ] = "Billing@BILLING-01",
            [Headers.HostId] = "not-a-guid"
        });

        yield return Case("null-valued addresses", new()
        {
            [Headers.OriginatingAddress] = null,
            [FaultsHeaderKeys.FailedQ] = null
        });

        yield return Case("no endpoint headers", new()
        {
            [Headers.MessageId] = "message-1"
        });
    }

    static TestCaseData Case(string name, Dictionary<string, string> headers) => new TestCaseData(headers).SetArgDisplayNames(name);
}
