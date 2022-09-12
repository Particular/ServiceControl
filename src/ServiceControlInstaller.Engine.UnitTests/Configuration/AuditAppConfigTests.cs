namespace ServiceControlInstaller.Engine.UnitTests.Configuration.ServiceControl
{
    using System;
    using System.IO;
    using Engine.Configuration.ServiceControl;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    public class AuditAppConfigTests
    {
        [Test]
        public void VerifySavedConfigFile()
        {
            var instance = new FakeAuditInstance
            {
                InstallPath = TestContext.CurrentContext.TestDirectory,
                TransportPackage = new TransportInfo(),
                Version = new Version(1, 0, 0)
            };

            var auditConfig = new ServiceControlAuditAppConfig(instance);

            auditConfig.Save();

            var configFile = File.ReadAllText(auditConfig.Config.FilePath);

            Approver.Verify(configFile);
        }

        class FakeAuditInstance : IServiceControlAuditInstance
        {
            public string AuditQueue { get; set; }

            public string AuditLogQueue { get; set; }

            public string VirtualDirectory { get; set; }

            public bool ForwardAuditMessages { get; set; }

            public TimeSpan AuditRetentionPeriod { get; set; }

            public string ServiceControlQueueAddress { get; set; }

            public bool EnableFullTextSearchOnBodies { get; set; }

            public int Port { get; set; }

            public string HostName { get; set; }

            public int? DatabaseMaintenancePort { get; set; }

            public string Name { get; set; }

            public string DisplayName { get; set; }

            public string ServiceAccount { get; set; }

            public string ServiceAccountPwd { get; set; }

            public Version Version { get; set; }

            public string DBPath { get; set; }

            public string InstallPath { get; set; }

            public string LogPath { get; set; }

            public bool SkipQueueCreation { get; set; }

            public TransportInfo TransportPackage { get; set; }

            public string ConnectionString { get; set; }
        }
    }
}