namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using NUnit.Framework;

    [TestFixture]
    class SelfContainedRavenDBTest
    {
        [Test]
        public void CheckForSelfContainedRavenDB()
        {
            bool isCI = Environment.GetEnvironmentVariable("CI") == "true";
            bool isLocal = !isCI;

            var ravenServerPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "deploy", "RavenDBServer"));
            var ravenStudio = Path.Combine(ravenServerPath, "Raven.Studio.zip");
            var ravenServerDll = Path.Combine(ravenServerPath, "Raven.Server.dll");
            var ravenServerExe = Path.Combine(ravenServerPath, "Raven.Server.exe");
            var offlineOperationsUtility = Path.Combine(ravenServerPath, "rvn.exe");

            Assert.Multiple(() =>
            {
                Assert.That(ravenStudio, Does.Exist); // No matter what
                Assert.That(Directory.Exists(ravenServerDll), Is.EqualTo(isLocal));  // Only in local development
                Assert.That(File.Exists(ravenServerExe), Is.EqualTo(isCI)); // Only on CI
                Assert.That(File.Exists(offlineOperationsUtility), Is.EqualTo(isCI));    // Only on CI
            });
        }
    }
}
