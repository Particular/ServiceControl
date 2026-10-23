namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using NuGet.Versioning;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;

    [TestFixture]
    public class UpgradeInfoTests
    {
        [TestCase("6.21.0")]
        [TestCase("6.21.0-beta.1")]
        [TestCase("6.21.2")]
        [TestCase("6.22.0")]
        [TestCase("7.0.0")]
        public void Should_accept_the_latest_version_of_major_6_and_later(string current)
        {
            var upgradeInfo = UpgradeInfo.GetUpgradePathFor(SemanticVersion.Parse(current));

            Assert.Multiple(() =>
            {
                Assert.That(upgradeInfo.HasIncompatibleVersion, Is.False);
                Assert.That(upgradeInfo.UpgradePath, Is.Empty);
            });
        }

        [TestCase("6.0.0", "6.21.0")]
        [TestCase("6.20.0", "6.21.0")]
        public void Should_require_the_latest_version_of_major_6_from_an_older_6(string current, string expectedPath)
        {
            var upgradeInfo = UpgradeInfo.GetUpgradePathFor(SemanticVersion.Parse(current));

            Assert.Multiple(() =>
            {
                Assert.That(upgradeInfo.HasIncompatibleVersion, Is.True);
                Assert.That(upgradeInfo.ToString(), Is.EqualTo(expectedPath));
            });
        }

        [TestCase("5.11.0", "6.21.0")]
        [TestCase("5.11.11", "6.21.0")]
        [TestCase("5.0.0", "5.11.0 ➡️ 6.21.0")]
        [TestCase("4.33.0", "5.11.0 ➡️ 6.21.0")]
        [TestCase("4.26.0", "4.33.0 ➡️ 5.11.0 ➡️ 6.21.0")]
        [TestCase("1.0.0", "1.48.0 ➡️ 2.1.5 ➡️ 3.8.4 ➡️ 4.33.0 ➡️ 5.11.0 ➡️ 6.21.0")]
        public void Should_keep_requiring_the_last_version_of_each_earlier_major(string current, string expectedPath)
        {
            var upgradeInfo = UpgradeInfo.GetUpgradePathFor(SemanticVersion.Parse(current));

            Assert.Multiple(() =>
            {
                Assert.That(upgradeInfo.HasIncompatibleVersion, Is.True);
                Assert.That(upgradeInfo.ToString(), Is.EqualTo(expectedPath));
            });
        }
    }
}
