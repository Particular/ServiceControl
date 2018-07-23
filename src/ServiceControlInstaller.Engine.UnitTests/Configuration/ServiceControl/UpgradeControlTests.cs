namespace ServiceControlInstaller.Engine.UnitTests.Configuration.ServiceControl
{
    using System;
    using Engine.Configuration.ServiceControl;
    using NUnit.Framework;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test]
        public void TestGetUpgradeInfoForTargetVersion()
        {
            var upgrade1Info = new UpgradeInfo(new Version(1, 5, 0), new Version(1, 0, 0))
            {
                RecommendedUpgradeVersion = new Version(1, 4, 9)
            };

            var upgrade2Info = new UpgradeInfo(new Version(2, 0, 0), new Version(1, 5, 0))
            {
                RecommendedUpgradeVersion = new Version(1, 9, 0)
            };

            UpgradeControl.details = new[]
            {
                upgrade1Info,
                upgrade2Info
            };

            var target = new Version(4, 0, 0);

            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(0, 1, 0));
            Assert.AreSame(upgrade1Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade1Info (below current)");

            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(1, 4, 0));
            Assert.AreSame(upgrade1Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade1Info (above current)");

            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(1, 5, 0));
            Assert.AreSame(upgrade2Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade2Info");

            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(2, 0, 0));

            var defaultUpgradeInfo = new UpgradeInfo(target, new Version(0, 0));
            Assert.AreEqual(defaultUpgradeInfo.TargetMinimumVersion, upgradeInfo.TargetMinimumVersion, $"TargetMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.CurrentMinimumVersion, upgradeInfo.CurrentMinimumVersion, $"CurrentMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.RecommendedUpgradeVersion, upgradeInfo.RecommendedUpgradeVersion, $"RecommendedUpgradeVersion mismatch {target}");

            target = upgrade2Info.TargetMinimumVersion;
            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(2, 0, 0));
            defaultUpgradeInfo = new UpgradeInfo(target, new Version(0, 0));
            Assert.AreEqual(defaultUpgradeInfo.TargetMinimumVersion, upgradeInfo.TargetMinimumVersion, $"TargetMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.CurrentMinimumVersion, upgradeInfo.CurrentMinimumVersion, $"CurrentMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.RecommendedUpgradeVersion, upgradeInfo.RecommendedUpgradeVersion, $"RecommendedUpgradeVersion mismatch {target}");
        }
    }
}