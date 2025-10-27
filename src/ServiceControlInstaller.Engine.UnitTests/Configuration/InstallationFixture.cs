namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.IO;
    using NUnit.Framework;

    public abstract class InstallationFixture
    {
        [SetUp]
        public void SetUp()
        {
            testPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID);

            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }

            InstallPath = Path.Combine(testPath, "install");
            DbPath = Path.Combine(testPath, "db");
            LogPath = Path.Combine(testPath, "log");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }
        }

        protected string InstallPath;

        protected string DbPath;

        protected string LogPath;

        string testPath;
    }
}
