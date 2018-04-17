namespace ServiceControlInstaller.Engine.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.ReportCard;
    using ServiceControlInstaller.Engine.Unattended;

    [TestFixture]
    public class RunEngine
    {
        const string DeploymentCache = @"..\..\..\..\Zip";

        [Test, Explicit]
        public void DeleteInstance()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            foreach (var instance in InstanceFinder.ServiceControlInstances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Delete(instance.Name, true, true);
            }
        }

        [Test, Explicit]
        public void UpgradeInstance()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            foreach (var instance in InstanceFinder.ServiceControlInstances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Upgrade(instance, new ServiceControlUpgradeOptions { AuditRetentionPeriod = TimeSpan.FromDays(30), ErrorRetentionPeriod = TimeSpan.FromDays(15), OverrideEnableErrorForwarding = true });
            }
        }

        [Test, Explicit]
        public void CreateInstanceMSMQ()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger(), DeploymentCache);
            var instanceName = "Test.ServiceControl.Msmq";
            var root = Path.Combine(@"c:\Test", instanceName);
            // ReSharper disable once UseObjectOrCollectionInitializer
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
                MaintenancePort = 33336,
                VirtualDirectory = null,
                AuditQueue = "audittest",
                ForwardAuditMessages = false,
                ForwardErrorMessages = false,
                AuditRetentionPeriod = TimeSpan.FromHours(SettingConstants.AuditRetentionPeriodDefaultInHoursForUI),
                ErrorRetentionPeriod = TimeSpan.FromDays(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI),
                ErrorQueue = "testerror",
                TransportPackage = "MSMQ",
                ReportCard = new ReportCard(),
            };

            // constructer of ServiceControlInstanceMetadata extracts version from zip
            // but this fails for unit tests as the deploymentCache path is not used
            details.Version = installer.ZipInfo.Version;

            details.Validate(s => false);
            if (details.ReportCard.HasErrors)
            {
                throw new Exception($"Validation errors:  {string.Join("\r\n", details.ReportCard.Errors)}");
            }
            Assert.DoesNotThrow(() => installer.Add(details, s => false));
        }

        [Test, Explicit]
        public void ChangeConfigTests()
        {
            var logger = new TestLogger();
            var installer = new UnattendServiceControlInstaller(logger, DeploymentCache);

            logger.Info("Deleting instances");
            DeleteInstance();

            logger.Info("Removing the test queue instances");
            RemoveAltMSMQQueues();

            logger.Info("Recreating the MSMQ instance");
            CreateInstanceMSMQ();

            logger.Info("Changing the URLACL");
            var msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.HostName = Environment.MachineName;
            msmqTestInstance.Port = 33338;
            msmqTestInstance.MaintenancePort = 33339;
            installer.Update(msmqTestInstance, true);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update URL change failed");

            logger.Info("Changing LogPath");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.LogPath = @"c:\temp\testloggingchange";
            installer.Update(msmqTestInstance, true);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update Logging changed failed");

            logger.Info("Updating Queue paths");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.AuditQueue = "alternateAudit";
            msmqTestInstance.ErrorQueue = "alternateError";
            installer.Update(msmqTestInstance, true);
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
    }
}