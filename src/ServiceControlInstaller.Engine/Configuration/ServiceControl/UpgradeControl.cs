namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Linq;

    public class UpgradeInfo
    {
        internal static UpgradeInfo[] details =
        {
            new UpgradeInfo(new Version(2, 0), new Version(1, 41, 3)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(1, 48, 0),
            },
            new UpgradeInfo(new Version(3, 0), new Version(2, 1, 3)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(2, 1, 3),
            },
            new UpgradeInfo(new Version(4, 0), new Version(3, 8, 2)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(3, 8, 2),
            },
            new UpgradeInfo(new Version(5, 0), new Version(5, 0, 0)) //No data migration from RavenDB 3.5 to 5. Can't upgrade in place
            {
                RecommendedUpgradeVersion = new Version(5, 0, 0),
            }
        };

        public static UpgradeInfo GetUpgradeInfoForTargetVersion(Version target, Version current)
        {
            return details.Where(r => r.TargetMinimumVersion <= target && current < r.CurrentMinimumVersion)
                .DefaultIfEmpty(new UpgradeInfo(target, new Version(0, 0)))
                .OrderBy(r => r.CurrentMinimumVersion)
                .First();
        }

        public UpgradeInfo(Version targetMinimum, Version currentMinimum)
        {
            TargetMinimumVersion = ConvertToCleanVersion(targetMinimum);
            CurrentMinimumVersion = ConvertToCleanVersion(currentMinimum);
        }

        /// <summary>
        /// Minimum version targeted for install that will trigger this
        /// </summary>
        public Version TargetMinimumVersion { get; }

        /// <summary>
        /// Minimum version instance must be to satisfy target version pre-conditions
        /// </summary>
        public Version CurrentMinimumVersion { get; }

        /// <summary>
        /// Version recommended for upgrade if instance version does not meet the CurrentMinimumVersion
        /// </summary>
        public Version RecommendedUpgradeVersion
        {
            get => recommendedUpgradeVersion ?? TargetMinimumVersion;
            set => recommendedUpgradeVersion = ConvertToCleanVersion(value);
        }

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
        public override bool Equals(object obj)
        {
            return obj is UpgradeInfo info && Equals(info);
        }

        bool Equals(UpgradeInfo val)
        {
            return ConvertToCleanVersion(val.TargetMinimumVersion) == TargetMinimumVersion &&
                   ConvertToCleanVersion(val.CurrentMinimumVersion) == CurrentMinimumVersion &&
                   ConvertToCleanVersion(val.RecommendedUpgradeVersion) == RecommendedUpgradeVersion;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 17 *
                       (23 + TargetMinimumVersion.GetHashCode()) *
                       (23 + CurrentMinimumVersion.GetHashCode()) *
                       (23 + RecommendedUpgradeVersion.GetHashCode());
            }
        }

        Version recommendedUpgradeVersion;
    }
}