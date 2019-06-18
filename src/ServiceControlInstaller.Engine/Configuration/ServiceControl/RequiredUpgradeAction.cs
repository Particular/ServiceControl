namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using Instances;

    public enum RequiredUpgradeAction
    {
        Upgrade,
        ConvertToAudit,
        SplitOutAudit
    }

    public static class RequiredUpgradeExtensions
    {
        public static RequiredUpgradeAction GetRequiredUpgradeAction(this ServiceControlInstance instance, Version target)
        {
            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, instance.Version);

            if (instance.Version < upgradeInfo.CurrentMinimumVersion)
            {
                // This is an older version
                return RequiredUpgradeAction.Upgrade;
            }

            if (instance.Version < VersionWhereAuditInstanceWasIntroduced && target >= VersionWhereAuditInstanceWasIntroduced)
            {
                // Introducing an Audit Instance
                if (IsDisabled(instance.AuditQueue))
                {
                    // This may be a master instance or just a standalone with Audit disabled
                    return RequiredUpgradeAction.Upgrade;
                }

                if (IsDisabled(instance.ErrorQueue))
                {
                    // This is an audit ingestor
                    return RequiredUpgradeAction.ConvertToAudit;
                }

                // This is a normal instance with both ingestors running
                return RequiredUpgradeAction.SplitOutAudit;
            }

            // This is presumed to be a normal instance that has audits split out already
            return RequiredUpgradeAction.Upgrade;
        }

        private static bool IsDisabled(string queueName) => queueName == "!disable";

        private static Version VersionWhereAuditInstanceWasIntroduced = new Version(4, 0, 0);
    }

}