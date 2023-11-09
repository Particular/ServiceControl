namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;
    using System.Linq;

    public class UpgradeControl
    {
        static readonly Version[] LatestMajors =
        {
            new(1, 48, 0),
            new(2, 1, 5),
            new(3, 8, 4),
            new(4, 33, 3),
        };

        public static Version[] GetUpgradePathFor(Version current) //5.0.0 // 4.24.0
        {
            return LatestMajors
                .Where(x => x > current)
                .ToArray();
        }

        public static bool HasIncompatibleVersion(Version version)
        {
            return LatestMajors.Last() <= version;
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