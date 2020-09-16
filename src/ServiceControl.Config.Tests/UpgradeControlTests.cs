namespace ServiceControl.Config.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Config.UI.Shell;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test, Explicit]
        public void GetUpgradeInfoForTargetVersionSameMajor()
        {
            Version current = new Version("4.0.1");

            Release releaseDetails = VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }

        [Test, Explicit]
        public void GetUpgradeInfoForTargetVersionFromNoAvailableUpgrade()
        {
            Version current = new Version("2.0.1");

            Release releaseDetails = VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current == releaseDetails.Version, "We didn't get the expected version, it shuld be current");
            Assert.IsNull(releaseDetails.Assets, "We expected to get the current vesion with no other info");
        }

        [Test, Explicit]
        public void TestGetUpgradeInfoForTargetVersionFromLowest()
        {
            Version current = new Version("3.8.0");

            Release releaseDetails = VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }

        [Test, Explicit]
        public void TestGetUpgradeInfoForTargetVersionFromLower()
        {
            Version current = new Version("3.8.3");

            Release releaseDetails = VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }

        [Test, Explicit]
        public void TestGetUpgradeInfoForTargetVersionVersionNotInTheList()
        {
            Version current = new Version("3.8.9");

            Release releaseDetails = VersionCheckerHelper.GetDownloadUrlForNextVersionToUpdate(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }
    }
}