namespace ServiceControlInstaller.Engine.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Engine.Configuration.ServiceControl;
    using Instances;
    using NUnit.Framework;
    using ReportCard;
    using Unattended;

    [TestFixture]
    public class RunEngine
    {
        [Test]
        [Explicit]
        public void DeleteInstance()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            foreach (var instance in InstanceFinder.ServiceControlInstances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Delete(instance.Name, true, true);
            }
        }

        [Test]
        [Explicit]
        public void UpgradeInstance()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            foreach (var instance in InstanceFinder.ServiceControlInstances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Upgrade(instance, new ServiceControlUpgradeOptions
                {
                    AuditRetentionPeriod = TimeSpan.FromDays(30),
                    ErrorRetentionPeriod = TimeSpan.FromDays(15),
                    OverrideEnableErrorForwarding = true
                });
            }
        }

        [Test]
        [Explicit]
        public async Task CreateInstanceMSMQ()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            var instanceName = "Test.ServiceControl.Msmq";
            var root = Path.Combine(@"c:\Test", instanceName);
            var details = new ServiceControlNewInstance
            {
                DisplayName = instanceName.Replace(".", " "),
                Name = instanceName,
                ServiceDescription = "Test SC Instance",
                DBPath = Path.Combine(root, "Database"),
                LogPath = Path.Combine(root, "Logs"),
                InstallPath = Path.Combine(root, "Binaries"),
                HostName = "localhost",
                Port = 33335,
                DatabaseMaintenancePort = 33336,
                VirtualDirectory = null,
                AuditQueue = "audittest",
                ForwardAuditMessages = false,
                ForwardErrorMessages = false,
                //TODO: Fix
                //AuditRetentionPeriod = TimeSpan.FromHours(SettingConstants.AuditRetentionPeriodDefaultInHoursForUI),
                ErrorRetentionPeriod = TimeSpan.FromDays(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI),
                ErrorQueue = "testerror",
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ReportCard = new ReportCard()
            };

            // constructer of ServiceControlInstanceMetadata extracts version from zip
            // but this fails for unit tests as the deploymentCache path is not used
            details.Version = installer.ZipInfo.Version;

            await details.Validate(s => Task.FromResult(false)).ConfigureAwait(false);
            if (details.ReportCard.HasErrors)
            {
                throw new Exception($"Validation errors:  {string.Join("\r\n", details.ReportCard.Errors)}");
            }

            Assert.DoesNotThrowAsync(() => installer.Add(details, s => Task.FromResult(false)));
        }

        [Test]
        [Explicit]
        public async Task ChangeConfigTests()
        {
            var logger = new TestLogger();
            var installer = new UnattendServiceControlInstaller(logger, DeploymentCache);

            logger.Info("Deleting instances");
            DeleteInstance();

            logger.Info("Removing the test queue instances");
            RemoveAltMSMQQueues();

            logger.Info("Recreating the MSMQ instance");
            await CreateInstanceMSMQ().ConfigureAwait(false);

            logger.Info("Changing the URLACL");
            var msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.HostName = Environment.MachineName;
            msmqTestInstance.Port = 33338;
            msmqTestInstance.DatabaseMaintenancePort = 33339;
            await installer.Update(msmqTestInstance, true).ConfigureAwait(false);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update URL change failed");

            logger.Info("Changing LogPath");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.LogPath = @"c:\temp\testloggingchange";
            await installer.Update(msmqTestInstance, true).ConfigureAwait(false);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update Logging changed failed");

            logger.Info("Updating Queue paths");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.AuditQueue = "alternateAudit";
            msmqTestInstance.ErrorQueue = "alternateError";
            await installer.Update(msmqTestInstance, true).ConfigureAwait(false);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update Queues changed failed");
        }

        void RemoveAltMSMQQueues()
        {
            var removeThese = new[]
            {
                @"private$\alternateAudit",
                @"private$\alternateError"
            };

            var queues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            foreach (var queue in queues)
            {
                if (removeThese.Contains(queue.QueueName, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Removing {0}", queue.QueueName);
                    MessageQueue.Delete(@".\" + queue.QueueName);
                }
            }
        }

        const string DeploymentCache = @"..\..\..\..\Zip";
    }
}