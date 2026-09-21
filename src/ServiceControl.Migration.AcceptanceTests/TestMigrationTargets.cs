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
    public int BatchSizeFor(MigrationCategory category) =>
        category.Id == MigrationCategoryIds.EndpointSettings ? 2 : inner.BatchSizeFor(category);

    public Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default) =>
        category.Id == MigrationCategoryIds.EndpointSettings && ++endpointSettingsWrites == failingWrite
            ? throw new Exception("injected mid-category failure")
            : inner.Write(category, batch, checkpointToExtend, cancellationToken);

    public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => inner.Count(category, cancellationToken);

    public IReadOnlyCollection<string> SupportedCategoryIds => inner.SupportedCategoryIds;
}

// Holds the copy open at a moment a test can observe, which is the only way to ask what the API answers mid-copy.
sealed class ParkingMigrationTarget(IMigrationTarget inner, TaskCompletionSource parked, Task release) : IMigrationTarget
{
    int writes;

    public Task Open(CancellationToken cancellationToken = default) => inner.Open(cancellationToken);

    public int BatchSizeFor(MigrationCategory category) => inner.BatchSizeFor(category);

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
