namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

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
            var runtimes = Path.Combine(ravenServerPath, "runtimes");
            var systemCollections = Path.Combine(ravenServerPath, "System.Collections.dll");
            var aspNetCoreHttp = Path.Combine(ravenServerPath, "Microsoft.AspNetCore.Http.dll");

            FileAssert.Exists(ravenStudio); // No matter what
            Assert.That(Directory.Exists(runtimes), Is.EqualTo(isLocal));  // Only in local development
            Assert.That(File.Exists(systemCollections), Is.EqualTo(isCI)); // Only on CI
            Assert.That(File.Exists(aspNetCoreHttp), Is.EqualTo(isCI));    // Only on CI
        }
    }
}
