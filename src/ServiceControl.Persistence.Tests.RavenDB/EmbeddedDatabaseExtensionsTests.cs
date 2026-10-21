namespace ServiceControl.Persistence.Tests;

using System.IO;
using System.Linq;
using NUnit.Framework;
using TestHelper;

[TestFixture]
public class EmbeddedDatabaseExtensionsTests
{
    [Test]
    public void DeleteServerTempFiles_should_delete_only_files_of_the_given_process()
    {
        var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", Path.GetRandomFileName());
        var tempDirectory = Path.Combine(root, "RavenDBServer", "Temp");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string[] owned = ["ravendb-100-1-0000000000000000001.journal-a", "ravendb-100-1-compression.0000000000.buffers-b"];
            string[] others = ["ravendb-1000-1-0000000000000000001.journal-c", "ravendb-200-1-0000000000000000001.journal-d"];
            var binary = Path.Combine(root, "RavenDBServer", "Raven.Server.dll");

            foreach (var name in owned.Concat(others))
            {
                File.WriteAllText(Path.Combine(tempDirectory, name), string.Empty);
            }
            File.WriteAllText(binary, string.Empty);

            EmbeddedDatabaseExtensions.DeleteServerTempFiles(tempDirectory, 100);

            Assert.Multiple(() =>
            {
                Assert.That(owned.Select(n => Path.Combine(tempDirectory, n)), Has.None.Matches<string>(File.Exists));
                Assert.That(others.Select(n => Path.Combine(tempDirectory, n)), Has.All.Matches<string>(File.Exists));
                Assert.That(File.Exists(binary), Is.True);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void DeleteServerTempFiles_should_ignore_missing_directory() =>
        Assert.DoesNotThrow(() => EmbeddedDatabaseExtensions.DeleteServerTempFiles(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), 100));
}
