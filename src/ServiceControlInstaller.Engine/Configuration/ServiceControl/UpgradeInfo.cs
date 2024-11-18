namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System.Linq;
    using NuGet.Versioning;

    public class UpgradeInfo
    {
        static readonly SemanticVersion[] LastVersionForEachMajor =
        [
            new(1, 48, 0),
            new(2, 1, 5),
            new(3, 8, 4),
            new(4, 33, 0), // Added IngestErrorMessages setting to enable message replay during 4to5 upgrade guide scenario 
            new(5, 11, 0)
        ];

        public SemanticVersion[] UpgradePath { get; private init; }
        public bool HasIncompatibleVersion { get; private init; }

        public static UpgradeInfo GetUpgradePathFor(SemanticVersion current) //5.0.0 // 4.24.0
        {
            var upgradePath = LastVersionForEachMajor
                .Where(x => current.CompareTo(x, VersionComparison.Version) < 0)
                .ToArray();

            return new UpgradeInfo
            {
                UpgradePath = upgradePath,
                HasIncompatibleVersion = upgradePath.Length > 0
            };
        }

        public override string ToString() => string.Join<SemanticVersion>(" ➡️ ", UpgradePath);
    }
}
