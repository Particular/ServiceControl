﻿namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    public partial class UpgradeControl
    {
        internal static UpgradeInfo[] details =
        {
            new UpgradeInfo(new Version(2, 0), new Version(1, 41, 3)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(1, 48, 0),
                DataBaseUpdate = true,
                DeleteIndexes = true
            },
            new UpgradeInfo(new Version(3, 0), new Version(2, 1, 3)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(2, 1, 3),
                DataBaseUpdate = false,
                DeleteIndexes = false
            },
            new UpgradeInfo(new Version(4, 0), new Version(3, 8, 2)) //https://github.com/Particular/ServiceControl/issues/1228
            {
                RecommendedUpgradeVersion = new Version(3, 8, 2),
                DataBaseUpdate = false,
                DeleteIndexes = false
            }
        };
    }
}
