namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Infrastructure;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the report masks as one row in Settings. A row the target already holds is left as it is and counted as
/// already present.
/// </summary>
sealed class LicensingReportMasksWriter(IMigrationSqlDialect migrationDialect) : SettingRowWriter<List<string>>(migrationDialect, SettingKeys.ReportMasks)
{
    public override string CategoryId => MigrationCategoryIds.LicensingReportMasks;
}
