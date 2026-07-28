namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;

class ErrorIngestionConcurrencyTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Concurrent_writers_on_the_same_messages_neither_collide_nor_lose_attempts()
    {
        const int writers = 4;
        const int messages = 25;

        var seeds = Enumerable.Range(0, messages).Select(_ => new IngestedFailure()).ToArray();
        var baseTime = seeds[0].AttemptedAt;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            foreach (var seed in seeds)
            {
                var attempt = seed.NextAttempt(baseTime.AddMinutes(writer));
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(attempt.Context, attempt.ProcessingAttempt, attempt.Groups);
            }

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        foreach (var seed in seeds)
        {
            var row = await GetFailedMessage(seed.UniqueMessageId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(row.NumberOfProcessingAttempts, Is.EqualTo(writers), "every writer's distinct attempt must be counted exactly once");
                Assert.That(row.LastAttemptedAt, Is.EqualTo(baseTime.AddMinutes(writers - 1)), "the newest attempt must win regardless of commit order");
            }
        }
    }

    [Test]
    public async Task Concurrent_writers_recording_the_same_endpoint_insert_it_once()
    {
        const int writers = 8;

        var endpoint = new EndpointDetails { Name = $"Endpoint-{Guid.NewGuid():N}", HostId = Guid.NewGuid(), Host = "Host1" };

        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            await unitOfWork.Monitoring.RecordKnownEndpoint(new KnownEndpoint
            {
                EndpointDetails = endpoint,
                HostDisplayName = endpoint.Host,
                Monitored = false
            });

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        Assert.That(await GetKnownEndpoints([endpoint.GetDeterministicId()]), Has.Count.EqualTo(1));
    }
}
