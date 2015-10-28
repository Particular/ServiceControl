namespace ServiceControlInstaller.Engine.UnitTests.Zip
{
    using System;
    using System.IO;
    using System.Linq;
    using Ionic.Zip;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.FileSystem;

    [TestFixture]
    public class UnpackTest
    {
        string zipFilePath;
        DirectoryInfo workingFolder;

        [SetUp]
        public void Setup()
        {
            var identifier = Guid.NewGuid().ToString("N");
            zipFilePath = Path.Combine(Path.GetTempPath(), string.Format("{0}.zip", identifier));
            var folders = new [] { "a", "b", "c"};
            workingFolder =  Directory.CreateDirectory( Path.Combine(Path.GetTempPath(), identifier ));

            foreach (var folder in folders)
            {
                var sub = workingFolder.CreateSubdirectory(folder);
                using (var x = File.CreateText(Path.Combine(sub.FullName, string.Format("{0}root.txt", folder))))
                {
                    x.WriteLine("temp");
                }

                var subsub = sub.CreateSubdirectory(string.Format("{0}{0}", folder));
                using (var x = File.CreateText(Path.Combine(subsub.FullName, string.Format("{0}sub.txt", folder))))
                {
                    x.WriteLine("temp");
                }
            }

            using (var zip = new ZipFile())
            {
                zip.AddDirectory(workingFolder.FullName, null);
                zip.Save(zipFilePath);
            }
            Console.WriteLine(zipFilePath);

        }

        [Test]
        public void FilterUnzipTest()
        {
            workingFolder.Delete(true);
            Directory.CreateDirectory(workingFolder.FullName);
            FileUtils.UnzipToSubdirectory(zipFilePath, workingFolder.FullName, "B");
            FileUtils.UnzipToSubdirectory(zipFilePath, workingFolder.FullName, @"A\AA");
            Assert.True(workingFolder.GetFiles().Count() == 2, "Should only be two file asub and broot");
            Assert.True(workingFolder.GetFiles("asub*.txt").Count() == 1, "subfolder did not unpack to root");
            Assert.True(workingFolder.GetDirectories().Count() == 1, "Should only be one directory after unpack");
        }

        [TearDown]
        public void TearDown()
        {
            workingFolder.Delete(true);
            File.Delete(zipFilePath);
        }
    }
}
