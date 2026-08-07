namespace ServiceControl.Persistence.EFCore.Infrastructure;

// All keys of the shared settings table are listed here so that the keyspace stays visible in one place.
static class SettingKeys
{
    public const string TrialEndDate = "TrialEndDate";
    public const string BrokerMetadata = "BrokerMetadata";
    public const string AuditServiceMetadata = "AuditServiceMetadata";
    public const string ReportMasks = "ReportMasks";
    public const string LicensedEndpointDetails = "LicensedEndpointDetails";
}
