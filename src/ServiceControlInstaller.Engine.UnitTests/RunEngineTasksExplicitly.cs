namespace ServiceControlInstaller.Engine.UnitTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Unattended;

    [TestFixture]
    public class RunEngine
    {
        const string deploymentCache = @"..\..\Zip";

        [Test, Explicit]
        public void DeleteInstance()
        {
            var installer = new UnattendInstaller(new TestLogger(), deploymentCache);
            foreach (var instance in ServiceControlInstance.Instances().Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Delete(instance.Name, true, true);
            }
        }

        [Test, Explicit]
        public void UpgradeInstance()
        {
            var installer = new UnattendInstaller(new TestLogger(), deploymentCache);
            foreach (var instance in ServiceControlInstance.Instances())  //.Where(p => p.Name.StartsWith("Test.ServiceControl", StringComparison.OrdinalIgnoreCase)))
            {
                installer.Upgrade(instance);
            }
        }

        [Test, Explicit]
        public void CreateInstanceMSMQ()
        {
            var installer = new UnattendInstaller(new TestLogger(), deploymentCache);
            var instanceName = "Test.ServiceControl.Msmq";
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Test", instanceName);
            Assert.DoesNotThrow( () => installer.Add(new ServiceControlInstanceMetadata
            {
                DisplayName = instanceName.Replace(".", " "),
                Name = instanceName,
                ServiceDescription = "Test SC Instance",
                DBPath = Path.Combine(root, "Database"),
                LogPath = Path.Combine(root, "Logs"),
                InstallPath = Path.Combine(root, "Binaries"),
                HostName = "localhost",
                Port = 33335,
                VirtualDirectory = null,
                AuditLogQueue = "audit.log",
                AuditQueue = "audit",
                ForwardAuditMessages = false,
                ErrorQueue = "error",
                ErrorLogQueue = "error.log",
                TransportPackage = "MSMQ",
                ConnectionString = null
            }));
          
        }

        [Test, Explicit]
        public void CreateInstanceASQ()
        {
            var installer = new UnattendInstaller(new TestLogger(), deploymentCache);
            var instanceName = "Test.ServiceControl.AzureStorageQueue";
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Test", instanceName);
            Assert.DoesNotThrow(() => installer.Add(new ServiceControlInstanceMetadata
            {
                DisplayName = instanceName.Replace(".", " "),
                Name = instanceName,
                ServiceDescription = "Test SC Instance",
                DBPath = Path.Combine(root, "Database"),
                LogPath = Path.Combine(root, "Logs"),
                InstallPath = Path.Combine(root, "Binaries"),
                HostName = "localhost",
                Port = 33336,
                VirtualDirectory = null,
                AuditQueue = "asqaudit",
                ErrorQueue = "asqerror",
                ForwardAuditMessages = false,
                TransportPackage = "AzureStorageQueue",
                ConnectionString = "DefaultEndpointsProtocol=https;AccountName=gregbielleman;AccountKey=U4oOjbyXNAyAR55tsR1HAQGUz/l/ngRC2zaS+hKS4//q6u35W4KeHGmMUV2/hW4k10E7JvAZM6kNRz2pVpxILw==;"
            }));
            
        }

        [Test, Explicit]
        public void ChangeConfigTests()
        {
            var logger = new TestLogger();
            var installer = new UnattendInstaller(logger, deploymentCache);

            logger.Info("Deleting instances");
            DeleteInstance();

            logger.Info("Removing the test queue instances");
            RemoveAltMSMQQueues();

            logger.Info("Recreating the MSMQ instance");
            CreateInstanceMSMQ();

            logger.Info("Changing the URLACL");
            var msmqTestInstance = ServiceControlInstance.Instances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.HostName = Environment.MachineName;
            msmqTestInstance.Port = 33338;
            installer.Update(msmqTestInstance, true);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update URL change failed");

            logger.Info("Changing LogPath");
            msmqTestInstance = ServiceControlInstance.Instances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
            msmqTestInstance.LogPath = @"c:\temp\testloggingchange";
            installer.Update(msmqTestInstance, true);
            Assert.IsTrue(msmqTestInstance.Service.Status == ServiceControllerStatus.Running, "Update Logging changed failed");

            logger.Info("Updating Queue paths");
            msmqTestInstance = ServiceControlInstance.Instances().First(p => p.Name.Equals("Test.ServiceControl.MSMQ", StringComparison.OrdinalIgnoreCase));
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