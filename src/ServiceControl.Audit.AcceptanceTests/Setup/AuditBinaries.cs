namespace ServiceControl.Audit.AcceptanceTests.Setup
{
    using System;
    using System.IO;
    using System.Linq;
    using AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;

    public class AuditBinaries : IDisposable
    {
        readonly ServiceControlAuditNewInstance installer = null;

        public static AuditBinaries CopyAndConfigureForSqlT()
        {
            var tempRandomDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRandomDirectory);

            var dbInstanceForSqlT = SqlLocalDb.CreateNewIn(tempRandomDirectory);

            var transportInfo = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.SQLServer);

            return new AuditBinaries(tempRandomDirectory, transportInfo, dbInstanceForSqlT);
        }

        public SqlLocalDb SqlTDb { get; } = null;

        AuditBinaries(string workingDirectory, TransportInfo transportInfo, SqlLocalDb sqlLocalDb)
        {
            WorkingDirectory = workingDirectory;
            SqlTDb = sqlLocalDb;

            var deploymentCache = FindZipFolder();

            var zipInfo = ServiceControlAuditZipInfo.Find(deploymentCache);

            installer = new ServiceControlAuditNewInstance()
            {
                DBPath = Path.Combine(workingDirectory, "Database"),
                LogPath = Path.Combine(workingDirectory, "Logs"),
                InstallPath = Path.Combine(workingDirectory, "Binaries"),
                AuditQueue = "audittest",
                ForwardAuditMessages = false,
                ForwardErrorMessages = false,
                ErrorQueue = "testerror",
                TransportPackage = transportInfo,
                ConnectionString = SqlTDb.ConnectionString,
                ReportCard = new ReportCard(),
                AuditRetentionPeriod = TimeSpan.FromDays(1),
                ServiceControlQueueAddress = "Particular.ServiceControl",
                Version = zipInfo.Version
            };

            installer.CopyFiles(zipInfo.FilePath);
            installer.WriteConfigurationFile();
        }

        public void ExecuteSetupCommand()
        {
            installer.SetupInstance();

            var errors = string.Join(Environment.NewLine, installer.ReportCard.Errors);

            Assert.IsTrue(errors.Length == 0, $"ServiceControl.Audit.exe --setup command execution failed with the following errors: {errors}");
        }

        public string AuditQueueName => installer.AuditQueue;
        public string ErrorQueue => installer.AuditQueue;
        public string WorkingDirectory { get; } = string.Empty;

        string FindZipFolder()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateDirectories(directory).Any(d => d.EndsWith("Zip", StringComparison.InvariantCultureIgnoreCase)))
                {
                    return $@"{directory}\zip";
                }

                var parent = Directory.GetParent(directory);

                if (parent == null)
                {
                    throw new Exception("Unable to locate the folder named 'Zip' that contains the platform zip files for this build.");
                }

                directory = parent.FullName;
            }
        }

        public void Dispose()
        {
            SqlTDb.Detach();
            DirectoryDeleter.Delete(WorkingDirectory);
        }
    }

}
