namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

abstract class ErrorIngestionTestBase : PersistenceTestBase
{
    protected ErrorIngestionTestBase() =>
        RegisterServices = services => services.AddSingleton<IBodyStoragePersistence>(RecordedBodies);

    protected RecordingBodyStoragePersistence RecordedBodies { get; } = new();

    protected EFPersisterSettings EFSettings => (EFPersisterSettings)PersistenceSettings;

    protected void AdvanceClock(TimeSpan by) => PersistenceTestsContext.FakeTime.Advance(by);

    protected async Task InBatch(Func<IIngestionUnitOfWork, Task> record)
    {
        await using var unitOfWork = await UnitOfWorkFactory.StartNew();

        await record(unitOfWork);

        await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
    }

    protected Task Ingest(params IngestedFailure[] failures) =>
        InBatch(async unitOfWork =>
        {
            foreach (var failure in failures)
            {
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            }
        });

    protected Task ConfirmRetry(params string[] uniqueMessageIds) =>
        InBatch(async unitOfWork =>
        {
            foreach (var uniqueMessageId in uniqueMessageIds)
            {
                await unitOfWork.Recoverability.RecordSuccessfulRetry(uniqueMessageId);
            }
        });

    protected async Task<FailedMessageEntity> GetFailedMessage(Guid uniqueMessageId)
    {
        var row = await Query(dbContext => dbContext.FailedMessages.AsNoTracking().SingleOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId));

        Assert.That(row, Is.Not.Null, $"No failed message row for {uniqueMessageId}");

        return row;
    }

    protected Task<FailedMessageEntity> FindFailedMessage(Guid uniqueMessageId) =>
        Query(dbContext => dbContext.FailedMessages.AsNoTracking().SingleOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId));

    protected Task<List<FailedMessageGroupEntity>> GetGroups(Guid uniqueMessageId) =>
        Query(dbContext => dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(g => g.FailedMessageUniqueId == uniqueMessageId)
            .ToListAsync());

    protected Task<List<KnownEndpointEntity>> GetKnownEndpoints(IReadOnlyCollection<Guid> ids) =>
        Query(dbContext => dbContext.KnownEndpoints.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync());

    protected async Task Store<T>(params T[] entities) where T : class
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        dbContext.Set<T>().AddRange(entities);

        await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    }

    protected Task<int> CountRetryRows(Guid uniqueMessageId) =>
        Query(dbContext => dbContext.FailedMessageRetries.AsNoTracking().CountAsync(r => r.UniqueMessageId == uniqueMessageId));

    protected Task RunRetentionSweep() =>
        ServiceProvider.GetServices<IHostedService>().OfType<RetentionSweeper>().Single().SweepNow(TestContext.CurrentContext.CancellationToken);

    async Task<T> Query<T>(Func<ServiceControlDbContext, Task<T>> query)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await query(dbContext);
    }

    protected class RecordingBodyStoragePersistence : IBodyStoragePersistence
    {
        readonly List<StoredBody> written = [];
        readonly List<string> deleted = [];

        // Body ids whose deletion should throw, to exercise the sweep's tolerate-missing handling.
        public HashSet<string> FailDeleteFor { get; } = [];

        public IReadOnlyList<StoredBody> Written
        {
            get
            {
                lock (written)
                {
                    return [.. written];
                }
            }
        }

        public IReadOnlyList<string> Deleted
        {
            get
            {
                lock (deleted)
                {
                    return [.. deleted];
                }
            }
        }

        public Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
        {
            lock (written)
            {
                written.Add(new StoredBody(bodyId, body.ToArray(), contentType));
            }

            return Task.CompletedTask;
        }

        public Task<MessageBodyFileResult> ReadBody(string bodyId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default)
        {
            if (FailDeleteFor.Contains(bodyId))
            {
                throw new InvalidOperationException($"Simulated missing body for {bodyId}");
            }

            lock (deleted)
            {
                deleted.Add(bodyId);
            }

            return Task.CompletedTask;
        }

        public record StoredBody(string BodyId, byte[] Body, string ContentType);
    }
}
