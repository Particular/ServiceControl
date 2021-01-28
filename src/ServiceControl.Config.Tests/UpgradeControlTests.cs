namespace ServiceControl.Config.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Config.UI.Shell;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test]
        [Explicit]
        public async Task GetUpgradeInfoForTargetVersionSameMajor()
        {
            var current = new Version("4.13.3");

            var releaseDetails = await VersionCheckerHelper.GetLatestRelease(current.ToString()).ConfigureAwait(false);

            Assert.IsNotNull(releaseDetails, "Failed to get a release details");
            Assert.That(releaseDetails.Version, Is.GreaterThanOrEqualTo(current), "Got a lower version than current");
        }
    }
}