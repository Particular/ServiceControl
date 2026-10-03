namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

static class TestMigrationTargets
{
    public static void ParkFirstMigrationWrite(this WebApplicationBuilder builder, TaskCompletionSource parked, Task release) =>
        builder.DecorateMigrationTarget(inner => new ParkingMigrationTarget(inner, parked, release));

    public static void FailTheSecondMigrationWrite(this WebApplicationBuilder builder) =>
        builder.DecorateMigrationTarget(inner => new FaultingMigrationTarget(inner, failingWrite: 2));

    public static void HaltTheEndpointSettingsCategory(this WebApplicationBuilder builder) =>
        builder.DecorateMigrationTarget(inner => new FaultingMigrationTarget(inner, failingWrite: 1));

    public static void StopAfterTheFirstCategorySettles(this WebApplicationBuilder builder, CancellationTokenSource stopping)
    {
        var registered = builder.Services.Last(service => service.ServiceType == typeof(IMigrationCheckpointStore));

        if (registered.ImplementationType is null)
        {
            throw new InvalidOperationException(
                "IMigrationCheckpointStore is registered by a factory rather than by type, so the test decorator cannot rebuild the inner store. Register it as AddSingleton<IMigrationCheckpointStore, TStore>() or give the decorator a different seam.");
        }

        builder.Services.AddSingleton<IMigrationCheckpointStore>(provider =>
            new StoppingAfterTheFirstSettleCheckpointStore((IMigrationCheckpointStore)ActivatorUtilities.CreateInstance(provider, registered.ImplementationType), stopping));
    }

    public static void DecorateMigrationTarget(this WebApplicationBuilder builder, Func<IMigrationTarget, IMigrationTarget> decorate)
    {
        var registered = builder.Services.Last(service => service.ServiceType == typeof(IMigrationTarget));

        if (registered.ImplementationType is null)
        {
            throw new InvalidOperationException(
                "IMigrationTarget is registered by a factory rather than by type, so the test decorator cannot rebuild the inner target. Register it as AddSingleton<IMigrationTarget, TTarget>() or give the decorator a different seam.");
        }

        builder.Services.AddSingleton<IMigrationTarget>(provider =>
            decorate((IMigrationTarget)ActivatorUtilities.CreateInstance(provider, registered.ImplementationType)));
    }
}

// Throws on one EndpointSettings write, which is the only way to see what a copy does after a write has failed.
sealed class FaultingMigrationTarget(IMigrationTarget inner, int failingWrite) : IMigrationTarget
{
    int endpointSettingsWrites;

    public Task Open(CancellationToken cancellationToken = default) => inner.Open(cancellationToken);

    // Two rows a batch, so five settings take three writes and a failure on the second leaves exactly one batch committed.
    public Task<int> BatchSizeFor(MigrationCategory category, CancellationToken cancellationToken = default) =>
        category.Id == MigrationCategoryIds.EndpointSettings ? Task.FromResult(2) : inner.BatchSizeFor(category, cancellationToken);

    public Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default) =>
        category.Id == MigrationCategoryIds.EndpointSettings && ++endpointSettingsWrites == failingWrite
            ? throw new Exception("injected mid-category failure")
            : inner.Write(category, batch, checkpointToExtend, cancellationToken);

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => inner.Count(category, cancellationToken);

    public IReadOnlyCollection<string> SupportedCategoryIds => inner.SupportedCategoryIds;
}

// Stops the copy the moment the first category settles, which is the only way to stop it between two categories.
sealed class StoppingAfterTheFirstSettleCheckpointStore(IMigrationCheckpointStore inner, CancellationTokenSource stopping) : IMigrationCheckpointStore
{
    public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default) => inner.ReadAll(cancellationToken);

    public Task<MigrationCheckpoint> Read(string categoryId, CancellationToken cancellationToken = default) => inner.Read(categoryId, cancellationToken);

    public async Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var stored = await inner.Upsert(checkpoint, cancellationToken);

        if (stored.State.IsFinished())
        {
            await stopping.CancelAsync();
            throw new OperationCanceledException(stopping.Token);
        }

        return stored;
    }
}

// Holds the copy open at a moment a test can observe, which is the only way to ask what the API answers mid-copy.
sealed class ParkingMigrationTarget(IMigrationTarget inner, TaskCompletionSource parked, Task release) : IMigrationTarget
{
    int writes;

    public Task Open(CancellationToken cancellationToken = default) => inner.Open(cancellationToken);

    public Task<int> BatchSizeFor(MigrationCategory category, CancellationToken cancellationToken = default) => inner.BatchSizeFor(category, cancellationToken);

    public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref writes, 1) == 0)
        {
            parked.SetResult();
            await release.WaitAsync(cancellationToken);
        }

        return await inner.Write(category, batch, checkpointToExtend, cancellationToken);
    }

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => inner.Count(category, cancellationToken);

    public IReadOnlyCollection<string> SupportedCategoryIds => inner.SupportedCategoryIds;
}
