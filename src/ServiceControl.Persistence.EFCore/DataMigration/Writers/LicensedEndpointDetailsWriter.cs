namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using Infrastructure;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the licensed endpoint details file as one row in Settings. A row the target already holds is left as it is
/// and counted as already present.
/// </summary>
sealed class LicensedEndpointDetailsWriter(IMigrationSqlDialect migrationDialect) : SettingRowWriter<LicensedEndpointDetails>(migrationDialect, SettingKeys.LicensedEndpointDetails)
{
    public override string CategoryId => MigrationCategoryIds.LicensedEndpointDetails;
}
