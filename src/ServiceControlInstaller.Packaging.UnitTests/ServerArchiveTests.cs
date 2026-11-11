namespace Tests
{
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml.Linq;
    using NUnit.Framework;

    [TestFixture]
    public class ServerArchiveTests
    {
        [Test]
        public void RavenDBServer_archive_holds_the_RavenDB_6_2_server() =>
            AssertArchiveHoldsServer("RavenDBServer.zip", ReadRavenDB6Version());

        [Test]
        public void RavenDB7Server_archive_holds_the_RavenDB_7_2_server() =>
            AssertArchiveHoldsServer("RavenDB7Server.zip", ReadRavenDB7Version());

        [TestCase("RavenDBServer.zip")]
        [TestCase("RavenDB7Server.zip")]
        public void Server_archive_leaves_out_the_native_dependencies_ServiceControl_does_not_use(string archiveName)
        {
            using var archive = ZipFile.OpenRead(GetArchivePath(archiveName));
            var excluded = new[] { "libcurl.dll", "librdkafka.dll", "librdkafkacpp.dll", "onnxruntime.dll", "onnxruntime.lib", "onnxruntime_providers_shared.dll", "onnxruntime_providers_shared.lib", "onnxruntime-genai.dll", "onnxruntime-genai.lib" };

            var found = archive.Entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .Where(name => excluded.Contains(name) || name.StartsWith("LocalEmbeddings/"))
                .ToList();

            Assert.That(found, Is.Empty);
        }

        static void AssertArchiveHoldsServer(string archiveName, string expectedVersion)
        {
            var extractDirectory = Path.Combine(Path.GetTempPath(), "ServerArchiveTests", Path.GetRandomFileName());
            Directory.CreateDirectory(extractDirectory);

            try
            {
                using var archive = ZipFile.OpenRead(GetArchivePath(archiveName));
                var entry = archive.GetEntry("Raven.Server.dll");
                Assert.That(entry, Is.Not.Null, $"{archiveName} does not contain Raven.Server.dll");

                var serverAssembly = Path.Combine(extractDirectory, "Raven.Server.dll");
                entry.ExtractToFile(serverAssembly);

                Assert.That(FileVersionInfo.GetVersionInfo(serverAssembly).ProductVersion, Does.StartWith(expectedVersion), $"{archiveName} holds the wrong RavenDB server version");
            }
            finally
            {
                Directory.Delete(extractDirectory, true);
            }
        }

        static string GetArchivePath(string archiveName)
        {
            var path = Path.Combine(DeploymentPackage.GetDeployDirectory().Parent.FullName, "zip", archiveName);
            Assert.That(path, Does.Exist);
            return path;
        }

        static string ReadRavenDB6Version() =>
            ReadPackagesProps().Descendants("RavenDB6Version").Single().Value;

        static string ReadRavenDB7Version() =>
            ReadPackagesProps().Descendants("PackageVersion").Single(e => (string)e.Attribute("Include") == "RavenDB.Embedded").Attribute("Version").Value;

        static XElement ReadPackagesProps() =>
            XDocument.Load(Path.Combine(DeploymentPackage.GetDeployDirectory().Parent.FullName, "src", "Directory.Packages.props")).Root;
    }
}
