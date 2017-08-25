// ReSharper disable InconsistentNaming
namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    public class SettingConstants
    {
        public const int ErrorRetentionPeriodMaxInDays = 45;
        public const int ErrorRetentionPeriodMaxInHours = 1080;

        public const int ErrorRetentionPeriodMinInDays = 10;
        public const int ErrorRetentionPeriodMinInHours = 240;

        public const int AuditRetentionPeriodMaxInHours = 8760;
        public const int AuditRetentionPeriodMinInHours = 1;

        public const int AuditRetentionPeriodDefaultInHoursForUI = 720;

        public const int ErrorRetentionPeriodDefaultInDaysForUI = 15;
    }
}
