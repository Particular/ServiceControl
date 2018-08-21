namespace ServiceControlInstaller.Engine.UnitTests.Configuration.ServiceControl
{
    using System;
    using System.Linq;
    using Engine.Configuration.ServiceControl;
    using NUnit.Framework;

    [TestFixture]
    public class UpgradeControlDetailsTests
    {
        [Test]
        public void DetailsAreValid()
        {
            //No duplicates
            Assert.AreEqual(UpgradeControl.details.Length, UpgradeControl.details.GroupBy(d => d.TargetMinimumVersion).Count(), "Duplicate TargetMinimumVersion entries");

            //No overlaps
            foreach (var upgradeInfo in UpgradeControl.details)
            {
                var details = UpgradeControl.details.Where(d => d != upgradeInfo);

                var failures = details.Where(d => d.TargetMinimumVersion > upgradeInfo.TargetMinimumVersion && d.CurrentMinimumVersion < upgradeInfo.TargetMinimumVersion).ToList();
                Assert.IsEmpty(failures, $"{string.Join(",", failures)} overlap with {upgradeInfo}");
            }
        }

        [Test]
        public void ValidateVersion2UpgradeData()
        {
            var tooOldVersion = new Version(1, 41, 0);

            var minimumVersion = new Version(1, 41, 3);

            var recommendedVersion = new Version(1, 48, 0);

            var version2 = new Version(2, 0, 0);

            //Test current is too old
            var upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(version2, tooOldVersion);
            Assert.AreEqual(minimumVersion, upgradeInfo.CurrentMinimumVersion, "CurrentMinimumVersion mismatch");
            Assert.AreEqual(version2, upgradeInfo.TargetMinimumVersion, "TargetMinimumVersion mismatch");
            Assert.AreEqual(recommendedVersion, upgradeInfo.RecommendedUpgradeVersion, "RecommendedUpgradeVersion mismatch");
            Assert.IsTrue(upgradeInfo.DataBaseUpdate, "DataBaseUpdate is false");
            Assert.IsTrue(upgradeInfo.DeleteIndexes, "DeleteIndexes is false");
        }
    }
}