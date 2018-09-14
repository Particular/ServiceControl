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
            var upgrade1Info = new UpgradeInfo(new Version(2, 0, 0), new Version(1, 41, 3))
            {
                RecommendedUpgradeVersion = new Version(1, 48, 0)
            };

            var upgrade2Info = new UpgradeInfo(new Version(3, 0, 0), new Version(2, 1, 3))
            {
                RecommendedUpgradeVersion = new Version(2, 1, 3)
            };

            UpgradeControl.details = new[]
            {
                upgrade1Info,
                upgrade2Info
            };

            var target = new Version(3, 1, 1);

            var defaultUpgradeInfo = new UpgradeInfo(target, new Version(0, 0));

            // User is 0.1 and trying to update to 3.1.1. Expect require update to 1.41.3 or higher. (upgrade1Info)
            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(0, 1, 0));
            Assert.AreSame(upgrade1Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade1Info");

            // User is 1.42 and trying to update to 3.1.1. Expect require update to 2.1.3 or higher (upgrade2info)
            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(1, 42, 0));
            Assert.AreSame(upgrade2Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade2Info (below 2)");

            // User is 2.1.0 and trying to upgrade to 3.1.1. Expect require update to 2.1.3 or higher (upgrade2info)
            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(2, 1, 0));
            Assert.AreSame(upgrade2Info, upgradeInfo, "Incorrect UpgradeInfro, expected upgrade2Info (above 2)");

            // User is 2.2 and trying to upgrade to 3.1.1. Expect default.
            upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(target, new Version(2, 2, 0));
            Assert.AreEqual(defaultUpgradeInfo, upgradeInfo, "Incorrect UpgradeInfro, expected defaultUpgradeInfo");

            Assert.AreEqual(defaultUpgradeInfo.TargetMinimumVersion, upgradeInfo.TargetMinimumVersion, $"TargetMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.CurrentMinimumVersion, upgradeInfo.CurrentMinimumVersion, $"CurrentMinimumVersion mismatch {target}");
            Assert.AreEqual(defaultUpgradeInfo.RecommendedUpgradeVersion, upgradeInfo.RecommendedUpgradeVersion, $"RecommendedUpgradeVersion mismatch {target}");
        }
    }
}