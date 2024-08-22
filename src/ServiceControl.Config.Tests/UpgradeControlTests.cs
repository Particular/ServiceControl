namespace ServiceControl.Config.Tests
{
    using System.Threading.Tasks;
    using NuGet.Versioning;
    using NUnit.Framework;
    using ServiceControl.Config.UI.Shell;

    [TestFixture]
    public class UpgradeControlTests
    {
        [Test]
        [Explicit]
        public async Task GetUpgradeInfoForTargetVersionSameMajor()
        {
            var current = new SemanticVersion(4, 13, 3);

            var releaseDetails = await VersionCheckerHelper.GetLatestRelease(current);

            Assert.That(releaseDetails, Is.Not.Null, "Failed to get a release details");
            Assert.That(releaseDetails.Version, Is.GreaterThanOrEqualTo(current), "Got a lower version than current");
        }
    }
}