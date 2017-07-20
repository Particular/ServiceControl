namespace ServiceControlInstaller.Engine.UnitTests.Zip
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.FileSystem;

    [TestFixture]
    public class VersionTest
    {
        const string basefilename = "particular.servicecontrol-{0}.zip";

        [SetUp]
        public void Setup()
        {
            Cleanup();
            foreach (var tag in  new[] { "1.1.0", "1.9.0", "1.10.0", "1.10.1-unstable", "1.2-0" })
            {
                using (var x = File.CreateText(Path.Combine(Path.GetTempPath(), string.Format(basefilename, tag))))
                {
                    x.WriteLine("temp");
                }
            }
        }

        [Test]
        public void FindVersionTest()
        {
            var zipInfo = ServiceControlZipInfo.Find(Path.GetTempPath());
            Assert.True(zipInfo.Present, "No zip file found but should be");
            Assert.True(zipInfo.Version == new Version("1.10.0"), "Incorrect version of zip found");
        }

        [TearDown]
        public void TearDown()
        {
            Cleanup();
        }

        void Cleanup()
        {
            var dir = new DirectoryInfo(Path.GetTempPath());
            foreach (var file in dir.GetFiles(string.Format(basefilename, "*")).ToList())
            {
                file.Delete();
            }
        }
    }
}
