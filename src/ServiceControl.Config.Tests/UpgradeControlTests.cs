namespace ServiceControl.Config.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Config.UI.Shell;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test, Explicit]
        public async Task GetUpgradeInfoForTargetVersionSameMajor()
        {
            Version current = new Version("4.0.1");

            var releaseDetails = await VersionCheckerHelper.GetLatestRelease(current.ToString()).ConfigureAwait(false);

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.IsTrue(current < releaseDetails.Version, "Got a lower version than current");
        }
    }
}