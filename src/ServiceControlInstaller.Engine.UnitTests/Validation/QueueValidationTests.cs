namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.Collections.Generic;
    using Engine.Validation;
    using Instances;
    using NuGet.Versioning;
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

            public bool EnableIntegratedServicePulse { get; set; }

            public TimeSpan ErrorRetentionPeriod { get; set; }

            public TimeSpan? AuditRetentionPeriod { get; set; }

            public List<RemoteInstanceSetting> RemoteInstances { get; set; }

            public bool EnableFullTextSearchOnBodies { get; set; }

            public int Port { get; set; }

            public string HostName { get; set; }

            public int? DatabaseMaintenancePort { get; set; }

            public string Name { get; set; }

            public string InstanceName { get; set; }

            public string DisplayName { get; set; }

            public string ServiceAccount { get; set; }

            public string ServiceAccountPwd { get; set; }

            public SemanticVersion Version { get; set; }

            public string DBPath { get; set; }

            public string InstallPath { get; set; }

            public string LogPath { get; set; }

            public bool SkipQueueCreation { get; set; }

            public TransportInfo TransportPackage { get; set; }

            public PersistenceManifest PersistenceManifest { get; set; }

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

            public string InstanceName { get; set; }

            public string DisplayName { get; set; }

            public string ServiceAccount { get; set; }

            public string ServiceAccountPwd { get; set; }

            public SemanticVersion Version { get; set; }

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
                TransportPackage = ServiceControlCoreTransports.Find("MSMQ"),
                ErrorQueue = @"error",
                ErrorLogQueue = @"errorlog",
                ForwardErrorMessages = true
            };

            var instanceB = new FakeServiceControlInstance
            {
                TransportPackage = ServiceControlCoreTransports.Find("RabbitMQ.QuorumConventionalRouting"),
                ErrorQueue = @"RMQerror",
                ErrorLogQueue = @"RMQerrorlog",
                ForwardErrorMessages = true,
                ConnectionString = @"afakeconnectionstring"
            };

            instances =
            [
                instanceA,
                instanceB
            ];
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.ErrorLogQueue = "errorlog";
            newInstance.ErrorQueue = "error";

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = []
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreUniqueWithinInstance());
        }

        [Test]
        public void CheckChainingOfAuditQueues_ShouldSucceed()
        {
            var existingAudit = new FakeServiceControlAuditInstance
            {
                TransportPackage = ServiceControlCoreTransports.Find("MSMQ"),
                AuditQueue = @"audit"
            };

            var newInstance = ServiceControlAuditNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.AuditQueue = "audit";

            var validator = new QueueNameValidator(newInstance)
            {
                AuditInstances = [existingAudit]
            };

            Assert.DoesNotThrow(() => validator.CheckQueueNamesAreNotTakenByAnotherAuditInstance());
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldThrow()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.ErrorLogQueue = "error";
            newInstance.ErrorQueue = "error";
            newInstance.ForwardErrorMessages = true;

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = []
            };

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreUniqueWithinInstance());
            Assert.That(ex.Message, Does.Contain("Each of the queue names specified for a instance should be unique"));
        }

        [Test]
        public void CheckQueueNamesAreNotTakenByAnotherInstance_ShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.ErrorLogQueue = "errorlog2";
            newInstance.ErrorQueue = "error2";

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

            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.ErrorLogQueue = "errorlog";
            newInstance.ErrorQueue = "error";
            newInstance.ForwardErrorMessages = true;

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));

            expectedError = "The queue name for ErrorQueue is already assigned to another ServiceControl instance";

            // with default names
            var defaultInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();
            defaultInstance.ErrorQueue = "Error";

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

            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("RabbitMQ.QuorumConventionalRouting");
            newInstance.ErrorLogQueue = "errorlog";
            newInstance.ErrorQueue = "error";
            newInstance.ForwardErrorMessages = true;

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain(expectedError));

            expectedError = "The queue name for ErrorQueue is already assigned to another ServiceControl instance";

            // with default names
            var defaultInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            defaultInstance.ErrorQueue = "Error";

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
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.TransportPackage = ServiceControlCoreTransports.Find("RabbitMQ.QuorumConventionalRouting");
            newInstance.ErrorQueue = "RMQerror";
            newInstance.ErrorLogQueue = "RMQerrorlog";
            newInstance.ConnectionString = "afakeconnectionstring";
            newInstance.ForwardErrorMessages = true;

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

        List<IServiceControlInstance> instances;
    }
}
