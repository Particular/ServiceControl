namespace ServiceControlInstaller.Engine.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
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
            var installer = new UnattendServiceControlInstaller(new TestLogger());
            foreach (var instance in InstanceFinder.ServiceControlInstances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Delete(instance.Name, true, true);
            }
        }

        [Test]
        [Explicit]
        public void UpgradeInstance()
        {
            var installer = new UnattendServiceControlInstaller(new TestLogger());
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
            var installer = new UnattendServiceControlInstaller(new TestLogger());
            var instanceName = "Test.ServiceControl.Msmq";
            var root = Path.Combine(Path.GetTempPath(), instanceName);
            var details = ServiceControlNewInstance.CreateWithDefaultPersistence();

            details.DisplayName = instanceName.Replace(".", " ");
            details.Name = instanceName;
            details.ServiceDescription = "Test SC Instance";
            details.DBPath = Path.Combine(root, "Database");
            details.LogPath = Path.Combine(root, "Logs");
            details.InstallPath = Path.Combine(root, "Binaries");
            details.HostName = "localhost";
            details.Port = 33335;
            details.DatabaseMaintenancePort = 33336;
            details.VirtualDirectory = null;
            details.AuditQueue = "audittest";
            details.ForwardAuditMessages = false;
            details.ForwardErrorMessages = false;
            details.AuditRetentionPeriod = TimeSpan.FromDays(SettingConstants.AuditRetentionPeriodDefaultInDaysForUI);
            details.ErrorRetentionPeriod = TimeSpan.FromDays(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI);
            details.ErrorQueue = "testerror";
            details.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            details.ReportCard = new ReportCard();
            // but this fails for unit tests as the deploymentCache path is not used
            // constructer of ServiceControlInstanceMetadata extracts version from zip
            details.Version = Constants.CurrentVersion;

            await details.Validate(s => Task.FromResult(false));
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
            var installer = new UnattendServiceControlInstaller(logger);

            logger.Info("Deleting instances");
            DeleteInstance();

            logger.Info("Removing the test queue instances");
            RemoveAltMSMQQueues();

            logger.Info("Recreating the MSMQ instance");
            await CreateInstanceMSMQ();

            logger.Info("Changing the URLACL");
            var msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.HostName = Environment.MachineName;
            msmqTestInstance.Port = 33338;
            msmqTestInstance.DatabaseMaintenancePort = 33339;
            await installer.Update(msmqTestInstance, true);
            Assert.That(msmqTestInstance.Service.Status, Is.EqualTo(ServiceControllerStatus.Running), "Update URL change failed");

            logger.Info("Changing LogPath");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.LogPath = Path.Combine(Path.GetTempPath(), "testloggingchange");
            await installer.Update(msmqTestInstance, true);
            Assert.That(msmqTestInstance.Service.Status, Is.EqualTo(ServiceControllerStatus.Running), "Update Logging changed failed");

            logger.Info("Updating Queue paths");
            msmqTestInstance = InstanceFinder.ServiceControlInstances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.AuditQueue = "alternateAudit";
            msmqTestInstance.ErrorQueue = "alternateError";
            await installer.Update(msmqTestInstance, true);
            Assert.That(msmqTestInstance.Service.Status, Is.EqualTo(ServiceControllerStatus.Running), "Update Queues changed failed");
        }

        void RemoveAltMSMQQueues()
        {
            //var removeThese = new[]
            //{
            //    @"private$\alternateAudit",
            //    @"private$\alternateError"
            //};

            //var queues = MessageQueue.GetPrivateQueuesByMachine("localhost");
            //foreach (var queue in queues)
            //{
            //    if (removeThese.Contains(queue.QueueName, StringComparer.OrdinalIgnoreCase))
            //    {
            //        Console.WriteLine("Removing {0}", queue.QueueName);
            //        MessageQueue.Delete(@".\" + queue.QueueName);
            //    }
            //}

            throw new Exception("If you need to use these explicit tests, you can rewrite it to use a different message queue.");
        }
    }
}