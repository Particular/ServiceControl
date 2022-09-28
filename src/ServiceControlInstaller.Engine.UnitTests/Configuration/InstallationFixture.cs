namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public class InstallationFixture
    {
        [SetUp]
        public void SetUp()
        {
            ZipFileFolder = GetZipFolder();

            testPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID);

            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }

            InstallPath = Path.Combine(testPath, "install");
            DbPath = Path.Combine(testPath, "db");
            LogPath = Path.Combine(testPath, "log");

            ZipFilePath = ZipFileFolder.EnumerateFiles("*.zip")
                .Single(f => f.Name.Contains(".Audit"))
                .FullName;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }
        }

        static DirectoryInfo GetZipFolder()
        {
            var currentFolder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (currentFolder != null)
            {
                var file = currentFolder.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault();

                if (file != null)
                {
                    return new DirectoryInfo(Path.Combine(file.Directory.Parent.FullName, "zip"));
                }

                currentFolder = currentFolder.Parent;
            }

            throw new Exception("Cannot find zip folder");
        }

        protected DirectoryInfo ZipFileFolder;

        protected string InstallPath;

        protected string DbPath;

        protected string LogPath;

        protected string ZipFilePath;

        string testPath;
    }
}
