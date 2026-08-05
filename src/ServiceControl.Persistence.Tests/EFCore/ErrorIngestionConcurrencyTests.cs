namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
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
    public async Task Concurrent_writers_leave_only_the_newest_attempts_groups()
    {
        const int writers = 4;
        const int messages = 10;

        var seeds = Enumerable.Range(0, messages).Select(_ => new IngestedFailure()).ToArray();
        var baseTime = seeds[0].AttemptedAt;

        var groupsPerWriter = Enumerable.Range(0, writers)
            .Select(writer => new List<FailedMessage.FailureGroup>
            {
                new() { Id = Guid.NewGuid().ToString(), Title = $"Writer {writer}", Type = "Exception Type and Stack Trace" }
            })
            .ToArray();

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            await using var unitOfWork = await UnitOfWorkFactory.StartNew();

            foreach (var seed in seeds)
            {
                var attempt = seed.NextAttempt(baseTime.AddMinutes(writer), groupsPerWriter[writer]);
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(attempt.Context, attempt.ProcessingAttempt, attempt.Groups);
            }

            await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
        })));

        var newestGroupId = groupsPerWriter[writers - 1][0].Id;

        foreach (var seed in seeds)
        {
            var groups = await GetGroups(seed.UniqueMessageId);

            Assert.That(groups.Select(group => group.GroupId), Is.EqualTo(new[] { newestGroupId }),
                "only the newest attempt's groups may survive, regardless of commit order");
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
