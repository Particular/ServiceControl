namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    public partial class UpgradeControl
    {
        internal static UpgradeInfo[] details =
        {
            new (new Version(2, 0), new Version(1, 41, 3))
            {
                RecommendedUpgradeVersion = new Version(1, 48, 0),
            },
            new (new Version(3, 0), new Version(2, 1, 3))
            {
                RecommendedUpgradeVersion = new Version(2, 1, 3),
            },
            new (new Version(4, 0), new Version(3, 8, 2))
            {
                RecommendedUpgradeVersion = new Version(3, 8, 2),
            },
            new (new Version(5, 0), new Version(4, 0, 0))
            {
                RecommendedUpgradeVersion = new Version(4, 32, 3),
            }
        };
    }
}