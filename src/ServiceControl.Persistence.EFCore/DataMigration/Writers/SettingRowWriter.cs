namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes a category that the target stores as one JSON row in Settings.
/// A row already under that key is left alone and counted as already present.
/// </summary>
abstract class SettingRowWriter<TDocument>(IMigrationSqlDialect migrationDialect, string settingKey) : MigrationCategoryWriter<TDocument> where TDocument : class
{
    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<SettingEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Settings.LongCountAsync(setting => setting.Key == settingKey, cancellationToken);

    /// <summary>
    /// Builds the Settings row for one document, storing the whole document by default.
    /// Override it when the document holds the value rather than being it.
    /// </summary>
    protected virtual SettingEntity RowFor(string key, TDocument document) => SettingRowMapper.Map(key, document);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, TDocument Document)> documents, CancellationToken cancellationToken = default)
    {
        List<SettingEntity> rows = [.. documents.Select(document => RowFor(settingKey, document.Document))];

        return Task.FromResult(new PreparedBatch(async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count, rows.Count, [], Merges: []));
    }
}
