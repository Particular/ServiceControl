namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IMigrationSource : IAsyncDisposable
{
    Task Open(CancellationToken cancellationToken = default);

    Task<MigrationSourceDescription> Describe(CancellationToken cancellationToken = default);

    // Collection names as the source reports them, not migration categories
    Task<IReadOnlyDictionary<string, long>> CountCollections(MigrationSourceDatabase database, CancellationToken cancellationToken = default);
}
