namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Engine.Validation;
    using Instances;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;

    [TestFixture]
    public class QueueValidationTests
    {
        class FakeServiceControlInstance : IServiceControlInstance
        {
            public string ErrorQueue { get; set; }

            public string ErrorLogQueue { get; set; }

            public string VirtualDirectory { get; set; }

            public bool ForwardErrorMessages { get; set; }

            public TimeSpan ErrorRetentionPeriod { get; set; }

            public TimeSpan? AuditRetentionPeriod { get; set; }

            public List<RemoteInstanceSetting> RemoteInstances { get; set; }

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

            public string Url { get; set; }

            public string BrowsableUrl { get; set; }
        }

        class FakeServiceControlAuditInstance : IServiceControlAuditInstance
        {
            public string AuditQueue { get; set; }

            public string AuditLogQueue { get; set; }

            public string VirtualDirectory { get; set; }

            public bool ForwardAuditMessages { get; set; }

            public TimeSpan AuditRetentionPeriod { get; set; }

            public string ServiceControlQueueAddress { get; set; }

            public PersistenceManifest PersistenceManifest { get; set; }

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

        [SetUp]
        public void Init()
        {
            var instanceA = new FakeServiceControlInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ErrorQueue = @"error",
                ErrorLogQueue = @"errorlog",
                ForwardErrorMessages = true
            };

            var instanceB = new FakeServiceControlInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQClassicConventionalRoutingTopology || t.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology),
                ErrorQueue = @"RMQerror",
                ErrorLogQueue = @"RMQerrorlog",
                ForwardErrorMessages = true,
                ConnectionString = @"afakeconnectionstring"
            };

            instances = new List<IServiceControlInstance>
            {
                instanceA,
                instanceB
            };
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ErrorLogQueue = "errorlog",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = new List<IServiceControlInstance>()
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreUniqueWithinInstance());
        }

        [Test]
        public void CheckChainingOfAuditQueues_ShouldSucceed()
        {
            var existingAudit = new FakeServiceControlAuditInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                AuditQueue = @"audit"
            };

            var newInstance = ServiceControlAuditNewInstance.CreateWithDefaultPersistence(GetZipFolder().FullName);

            newInstance.TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ);
            newInstance.AuditQueue = "audit";

            var validator = new QueueNameValidator(newInstance)
            {
                AuditInstances = new List<IServiceControlAuditInstance> { existingAudit }
            };

            Assert.DoesNotThrow(() => validator.CheckQueueNamesAreNotTakenByAnotherAuditInstance());
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldThrow()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ErrorLogQueue = "error",
                ErrorQueue = "error",
                ForwardErrorMessages = true
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = new List<IServiceControlInstance>()
            };

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreUniqueWithinInstance());
            Assert.That(ex.Message, Does.Contain("Each of the queue names specified for a instance should be unique"));
        }

        [Test]
        public void CheckQueueNamesAreNotTakenByAnotherInstance_ShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ErrorLogQueue = "errorlog2",
                ErrorQueue = "error2"
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
        }

        [Test]
        public void CheckQueueNamesAreNotTakenByAnotherInstance_ShouldThrow()
        {
            var expectedError = "Some queue names specified are already assigned to another ServiceControl instance - Correct the values for ErrorLogQueue, ErrorQueue";
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                ErrorLogQueue = "errorlog",
                ErrorQueue = "error",
                ForwardErrorMessages = true
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));

            expectedError = "The queue name for ErrorQueue is already assigned to another ServiceControl instance";

            // with default names
            var defaultInstance = new ServiceControlNewInstance
            {
                ErrorQueue = "Error"
            };

            p = new QueueNameValidator(defaultInstance)
            {
                SCInstances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));
        }

        [Test]
        public void DuplicateQueueNamesAreAllowedOnDifferentTransports_ShouldNotThrow()
        {
            var expectedError = "Some queue names specified are already assigned to another ServiceControl instance - Correct the values for ErrorLogQueue, ErrorQueue";

            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology),
                ErrorLogQueue = "errorlog",
                ErrorQueue = "error",
                ForwardErrorMessages = true
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));

            expectedError = "The queue name for ErrorQueue is already assigned to another ServiceControl instance";

            // with default names
            var defaultInstance = new ServiceControlNewInstance
            {
                ErrorQueue = "Error"
            };
            p = new QueueNameValidator(defaultInstance)
            {
                SCInstances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));
        }

        [Test]
        public void EnsureDuplicateQueueNamesAreAllowedOnSameTransportWithDifferentConnectionString()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology),
                ErrorQueue = "RMQerror",
                ErrorLogQueue = "RMQerrorlog",
                ConnectionString = "afakeconnectionstring",
                ForwardErrorMessages = true
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));

            newInstance.ConnectionString = "differentconnectionstring";
            p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
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

        List<IServiceControlInstance> instances;
    }
}
