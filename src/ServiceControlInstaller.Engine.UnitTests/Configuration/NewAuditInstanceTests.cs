namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class NewAuditInstanceTests
    {
        [Test]
        public void Should_default_persistence_to_raven5()
        {
            var newInstance = new ServiceControlAuditNewInstance();

            StringAssert.Contains("RavenDb5", newInstance.PersistencePackage.TypeName);
        }

        [Test]
        public void Should_install_persister()
        {
            var newInstance = new ServiceControlAuditNewInstance
            {
                Version = new Version(1, 0, 0)
            };
            var installPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "install");
            var dbPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "db");
            var logPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "log");
            var zipFileFolder = GetZipFolder();

            var zipFilePath = zipFileFolder.EnumerateFiles("*.zip")
                .Single(f => f.Name.Contains(".Audit"))
                .FullName;

            newInstance.InstallPath = installPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.All.First();
            newInstance.DBPath = dbPath;
            newInstance.LogPath = logPath;

            newInstance.CopyFiles(zipFilePath);
            newInstance.WriteConfigurationFile();

            FileAssert.Exists(Path.Combine(installPath, "ServiceControl.Audit.Persistence.RavenDB5.dll"));
            var configFile = File.ReadAllText(Path.Combine(installPath, "ServiceControl.Audit.exe.config"));

            Approver.Verify(configFile, input => input.Replace(dbPath, "value-not-asserted").Replace(logPath, "value-not-asserted"));
        }

        public static DirectoryInfo GetZipFolder()
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
    }
}
