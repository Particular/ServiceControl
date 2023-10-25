﻿namespace ServiceControlInstaller.Engine.UnitTests.Zip
{
    using System;
    using System.IO;
    using System.Linq;
    using FileSystem;
    using Ionic.Zip;
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
            using (var zipStream = File.OpenRead(zipFilePath))
            {
                FileUtils.UnzipToSubdirectory(zipStream, workingFolder.FullName, "B");
            }
            using (var zipStream = File.OpenRead(zipFilePath))
            {
                FileUtils.UnzipToSubdirectory(zipStream, workingFolder.FullName, @"A\AA");
            }
            Assert.True(workingFolder.GetFiles().Count() == 2, "Should only be two file asub and broot");
            Assert.True(workingFolder.GetFiles("asub*.txt").Count() == 1, "subfolder did not unpack to root");
            Assert.True(workingFolder.GetDirectories().Count() == 1, "Should only be one directory after unpack");
        }

        [TestCase("")]
        [TestCase(null)]
        public void UnzipAllTest(string filter)
        {
            workingFolder.Delete(true);
            Directory.CreateDirectory(workingFolder.FullName);
            using (var zipStream = File.OpenRead(zipFilePath))
            {
                FileUtils.UnzipToSubdirectory(zipStream, workingFolder.FullName, filter);
            }
            Assert.True(workingFolder.GetDirectories().Count() == 3, "Should include all directories");
            Assert.True(workingFolder.GetFiles("*.txt", SearchOption.TopDirectoryOnly).Count() == 0, "Should have no files extracted to the root install path");
            Assert.True(workingFolder.GetFiles("*.txt", SearchOption.AllDirectories).Count() == 6, "Should include all 3 root and subfolder files");
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