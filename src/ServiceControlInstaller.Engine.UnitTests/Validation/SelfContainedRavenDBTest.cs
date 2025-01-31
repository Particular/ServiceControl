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

            var ravenServerPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "deploy", "RavenDBServer"));
            var ravenStudio = Path.Combine(ravenServerPath, "Raven.Studio.zip");
            var ravenServerDll = Path.Combine(ravenServerPath, "Raven.Server.dll");
            var ravenServerExe = Path.Combine(ravenServerPath, "Raven.Server.exe");
            var offlineOperationsUtility = Path.Combine(ravenServerPath, "rvn.exe");

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ravenStudio, Does.Exist); // As of 6.2.3 this exists in embedded & self-contained versions
                    Assert.That(ravenServerDll, Does.Exist);  // As of 6.2.3 this exists in embedded & self-contained versions
                    if (isCI)
                    {
                        // These may not exist on a local build and that's OK, but they must exist in a self-contained build
                        Assert.That(ravenServerExe, Does.Exist);
                        Assert.That(offlineOperationsUtility, Does.Exist);
                    }
                });
            }
            catch (Exception)
            {
                TestContext.Out.WriteLine($"Contents of RavenServerPath {ravenServerPath}:");
                foreach (var name in Directory.GetFileSystemEntries(ravenServerPath))
                {
                    TestContext.Out.WriteLine($" * {name}");
                }
                throw;
            }
        }
    }
}
