namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Monitoring;
    using NUnit.Framework;

    [TestFixture]
    class KnownEndpointsTests : PersistenceTestFixture
    {
        [Test]
        public async Task Basic_Roundtrip()
        {
            var ingestedEndpoint = new KnownEndpoint
            {
                Host = "HostName",
                HostId = Guid.NewGuid(),
                LastSeen = DateTime.UtcNow,
                Name = "Endpoint"
            };

            await IngestKnownEndpoints(ingestedEndpoint);

            var endpoints = await DataStore.QueryKnownEndpoints();

            Assert.That(endpoints.Results.Count, Is.EqualTo(1));
            var endpoint = endpoints.Results[0];
            Assert.That(endpoint.HostDisplayName, Is.EqualTo(ingestedEndpoint.Host));
            Assert.That(endpoint.EndpointDetails.Host, Is.EqualTo(ingestedEndpoint.Host));
            Assert.That(endpoint.EndpointDetails.HostId, Is.EqualTo(ingestedEndpoint.HostId));
            Assert.That(endpoint.EndpointDetails.Name, Is.EqualTo(ingestedEndpoint.Name));
        }

        [Test]
        public async Task Can_query_many_known_endpoints()
        {
            var knownEndpoints = Enumerable.Range(1, 200)
                .Select(x => new KnownEndpoint
                {
                    Host = $"HostName{x}",
                    HostId = Guid.NewGuid(),
                    Name = $"Endpoint{x}"
                }).ToArray();

            await IngestKnownEndpoints(knownEndpoints);

            var queryResult = await DataStore.QueryKnownEndpoints();

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(200));
            Assert.That(queryResult.Results.Count, Is.EqualTo(200));
        }

        async Task IngestKnownEndpoints(params KnownEndpoint[] knownEndpoints)
        {
            var unitOfWork = StartAuditUnitOfWork(knownEndpoints.Length);
            foreach (var knownEndpoint in knownEndpoints)
            {
                await unitOfWork.RecordKnownEndpoint(knownEndpoint)
                    ;
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }

    }
}