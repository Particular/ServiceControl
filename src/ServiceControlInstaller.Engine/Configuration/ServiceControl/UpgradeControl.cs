namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Linq;

    public partial class UpgradeControl
    {
        public static UpgradeInfo GetUpgradeInfoForTargetVersion(Version target, Version current)
        {
            return details.Where(r => r.TargetMinimumVersion <= target && current < r.TargetMinimumVersion)
                .DefaultIfEmpty(new UpgradeInfo(target, new Version(0, 0)))
                .OrderBy(r => r.CurrentMinimumVersion)
                .First();
        }
    }

    public class UpgradeInfo
    {
        /// <summary>
        /// Minimum version targeted for install that will trigger this
        /// </summary>
        public Version TargetMinimumVersion { get; }

        /// <summary>
        /// Minimum version instance must be to satisfy target version pre-conditions
        /// </summary>
        public Version CurrentMinimumVersion { get; }

        public UpgradeInfo(Version targetMinimum, Version currentMinimum)
        {
            TargetMinimumVersion = ConvertToCleanVersion(targetMinimum);
            CurrentMinimumVersion = ConvertToCleanVersion(currentMinimum);
        }

        /// <summary>
        /// Version recommended for upgrade if instance version does not meet the CurrentMinimumVersion
        /// </summary>
        public Version RecommendedUpgradeVersion
        {
            get => recommendedUpgradeVersion ?? TargetMinimumVersion;
            set => recommendedUpgradeVersion = ConvertToCleanVersion(value);
        }

        /// <summary>
        /// Version requires a database upgrade
        /// </summary>
        public bool DataBaseUpdate { get; set; }

        /// <summary>
        /// Inidicates indexes should be removed, prompting recreation, for this upgrade
        /// </summary>
        public bool DeleteIndexes { get; set; }

        Version recommendedUpgradeVersion;

        static Version ConvertToCleanVersion(Version version)
        {
            return new Version(
                version.Major > -1 ? version.Major : 0,
                version.Minor > -1 ? version.Minor : 0,
                version.Build > -1 ? version.Build : 0);
        }

        public override string ToString()
        {
            return $"[{CurrentMinimumVersion} -> {TargetMinimumVersion}]";
        }
    }
}
