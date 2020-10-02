namespace ServiceControl.Config.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Config.UI.Shell;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test]
        [Explicit]
        public void GetUpgradeInfoForTargetVersionSameMajor()
        {
            Version current = new Version("4.0.1");

            var releaseDetails = VersionCheckerHelper.GetLatestRelease(current.ToString()).Result;

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }
    }
}