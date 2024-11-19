namespace ServiceControlInstaller.Engine.UnitTests.Zip
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using FileSystem;
    using NUnit.Framework;

    [TestFixture]
    public class UnpackTest
    {
        [SetUp]
        public void Setup()
        {
            var identifier = Guid.NewGuid().ToString("N");
            zipFilePath = Path.Combine(Path.GetTempPath(), $"{identifier}.zip");
            var folders = new[]
            {
                "a",
                "b",
                "c"
            };
            workingFolder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), identifier));

            foreach (var folder in folders)
            {
                var sub = workingFolder.CreateSubdirectory(folder);
                using (var x = File.CreateText(Path.Combine(sub.FullName, $"{folder}root.txt")))
                {
                    x.WriteLine("temp");
                }

                var subsub = sub.CreateSubdirectory(string.Format("{0}{0}", folder));
                using (var x = File.CreateText(Path.Combine(subsub.FullName, $"{folder}sub.txt")))
                {
                    x.WriteLine("temp");
                }
            }

            ZipFile.CreateFromDirectory(workingFolder.FullName, zipFilePath);

            Console.WriteLine(zipFilePath);
        }

        [Test]
        public void UnzipTest()
        {
            workingFolder.Delete(true);
            Directory.CreateDirectory(workingFolder.FullName);
            using (var zipStream = File.OpenRead(zipFilePath))
            {
                FileUtils.UnzipToSubdirectory(zipStream, workingFolder.FullName);
            }
            Assert.That(workingFolder.GetDirectories().Count, Is.EqualTo(3), "Should include all directories");
            Assert.That(workingFolder.GetFiles("*.txt", SearchOption.TopDirectoryOnly).Length, Is.EqualTo(0), "Should have no files extracted to the root install path");
            Assert.That(workingFolder.GetFiles("*.txt", SearchOption.AllDirectories).Length, Is.EqualTo(6), "Should include all 3 root and subfolder files");
        }

        [TearDown]
        public void TearDown()
        {
            workingFolder.Delete(true);
            File.Delete(zipFilePath);
        }

        string zipFilePath;
        DirectoryInfo workingFolder;
    }
}