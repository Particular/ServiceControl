﻿namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Linq;

    public partial class UpgradeControl
    {
        public static UpgradeInfo GetUpgradeInfoForTargetVersion(Version target, Version current)
        {
            return details.Where(r => r.TargetMinimumVersion <= target && current < r.CurrentMinimumVersion)
                .DefaultIfEmpty(new UpgradeInfo(target, new Version(0, 0)))
                .OrderBy(r => r.CurrentMinimumVersion)
                .First();
        }
    }

    public class UpgradeInfo
    {
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