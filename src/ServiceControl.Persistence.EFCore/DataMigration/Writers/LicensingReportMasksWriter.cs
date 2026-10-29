namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the report masks as one row in Settings. A row the target already holds is left as it is and counted as
/// already present.
/// </summary>
sealed class LicensingReportMasksWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<List<string>>
{
    public override string CategoryId => MigrationCategoryIds.LicensingReportMasks;

    const string SettingKey = SettingKeys.ReportMasks;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<SettingEntity>(dbContext);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, List<string> Document)> documents, CancellationToken cancellationToken = default)
    {
        List<SettingEntity> rows = [.. documents.Select(document => SettingRowMapper.Map(SettingKey, document.Document))];

        return Task.FromResult(new PreparedBatch(async (context, token) => (await migrationDialect.InsertMissing(context, rows, token)).Count, rows.Count, [], Merges: []));
    }

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Settings.LongCountAsync(setting => setting.Key == SettingKey, cancellationToken);
}
