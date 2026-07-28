namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;

class ErrorIngestionKnownEndpointTests : ErrorIngestionTestBase
{
    [Test]
    public async Task An_unknown_endpoint_is_inserted_unmonitored()
    {
        var endpoint = NewEndpoint();

        await InBatch(unitOfWork => unitOfWork.Monitoring.RecordKnownEndpoint(new KnownEndpoint
        {
            EndpointDetails = endpoint,
            HostDisplayName = endpoint.Host,
            Monitored = false
        }));

        var stored = (await GetKnownEndpoints([endpoint.GetDeterministicId()])).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Name, Is.EqualTo(endpoint.Name));
            Assert.That(stored.HostId, Is.EqualTo(endpoint.HostId));
            Assert.That(stored.Host, Is.EqualTo(endpoint.Host));
            Assert.That(stored.Monitored, Is.False, "Endpoints discovered during ingestion are not monitored");
        }
    }

    [Test]
    public async Task A_known_endpoint_keeps_its_monitored_flag()
    {
        var endpoint = NewEndpoint();

        await Store(new KnownEndpointEntity
        {
            Id = endpoint.GetDeterministicId(),
            Name = endpoint.Name,
            HostId = endpoint.HostId,
            Host = endpoint.Host,
            Monitored = true
        });

        await InBatch(unitOfWork => unitOfWork.Monitoring.RecordKnownEndpoint(new KnownEndpoint
        {
            EndpointDetails = endpoint,
            HostDisplayName = endpoint.Host,
            Monitored = false
        }));

        var stored = (await GetKnownEndpoints([endpoint.GetDeterministicId()])).Single();

        Assert.That(stored.Monitored, Is.True, "Ingestion must never overwrite an existing endpoint");
    }

    [Test]
    public async Task The_same_endpoint_twice_in_one_batch_is_inserted_once()
    {
        var endpoint = NewEndpoint();

        await InBatch(async unitOfWork =>
        {
            for (var i = 0; i < 2; i++)
            {
                await unitOfWork.Monitoring.RecordKnownEndpoint(new KnownEndpoint
                {
                    EndpointDetails = endpoint,
                    HostDisplayName = endpoint.Host,
                    Monitored = false
                });
            }
        });

        Assert.That(await GetKnownEndpoints([endpoint.GetDeterministicId()]), Has.Count.EqualTo(1));
    }

    static EndpointDetails NewEndpoint() => new()
    {
        Name = $"Endpoint-{Guid.NewGuid():N}",
        HostId = Guid.NewGuid(),
        Host = "Host1"
    };
}
