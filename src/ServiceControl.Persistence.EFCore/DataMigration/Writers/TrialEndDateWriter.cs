namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Entities;
using Infrastructure;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the trial end date as one Settings row, storing only the date, as the product does.
/// </summary>
sealed class TrialEndDateWriter(IMigrationSqlDialect migrationDialect) : SettingRowWriter<TrialMetadata>(migrationDialect, SettingKeys.TrialEndDate)
{
    public override string CategoryId => MigrationCategoryIds.TrialEndDate;

    protected override SettingEntity RowFor(string key, TrialMetadata document) => SettingRowMapper.Map(key, document.TrialEndDate);
}
