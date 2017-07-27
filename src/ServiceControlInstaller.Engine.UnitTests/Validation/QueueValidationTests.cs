namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.Collections.Generic;
    using Moq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    [TestFixture]
    public class QueueValidationTests
    {
        List<IContainTransportInfo> instances;

        [SetUp]
        public void Init()
        {
            var instanceA = new Mock<IContainTransportInfo>();
            instanceA.SetupGet(p => p.TransportPackage).Returns(@"MSMQ");
            instanceA.SetupGet(p => p.AuditQueue).Returns(@"audit");
            instanceA.SetupGet(p => p.AuditLogQueue).Returns(@"auditlog");
            instanceA.SetupGet(p => p.ErrorQueue).Returns(@"error");
            instanceA.SetupGet(p => p.ErrorLogQueue).Returns(@"errorlog");

            var instanceB = new Mock<IContainTransportInfo>();
            instanceB.SetupGet(p => p.TransportPackage).Returns(@"RabbitMQ");
            instanceB.SetupGet(p => p.AuditQueue).Returns(@"RMQaudit");
            instanceB.SetupGet(p => p.AuditLogQueue).Returns(@"RMQauditlog");
            instanceB.SetupGet(p => p.ErrorQueue).Returns(@"RMQerror");
            instanceB.SetupGet(p => p.ErrorLogQueue).Returns(@"RMQerrorlog");
            instanceB.SetupGet(p => p.ConnectionString).Returns(@"afakeconnectionstring");

            instances = new List<IContainTransportInfo>
            {
                instanceA.Object,
                instanceB.Object
            };
        }

        [Test]
        public void CheckQueueNamesAreUnique_ShouldSucceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
               TransportPackage = "MSMQ",
               AuditLogQueue = "auditlog",
               ErrorLogQueue = "errorlog",
               AuditQueue =    "audit",
               ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = new List<IContainTransportInfo>()
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreUniqueWithinInstance());
        }

        [Test]
        public void CheckQueueNamesAreUnique_ShouldThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                TransportPackage = "MSMQ",
                AuditLogQueue = "audit",
                ErrorLogQueue = "error",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = new List<IContainTransportInfo>()
            };

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreUniqueWithinInstance());
            Assert.That(ex.Message, Is.StringContaining("Each of the queue names specified for a instance should be unique"));
        }

        [Test]
        public void CheckQueueNamesAreNotTakenByAnotherInstance_ShouldSucceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                TransportPackage = "MSMQ",
                AuditLogQueue = "auditlog2",
                ErrorLogQueue = "errorlog2",
                AuditQueue = "audit2",
                ErrorQueue = "error2"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = instances
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
        }

        [Test]
        public void CheckQueueNamesAreNotTakenByAnotherInstance_ShouldThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                TransportPackage = "MSMQ",
                AuditLogQueue = "auditlog",
                ErrorLogQueue = "errorlog",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
            Assert.That(ex.Message, Is.StringContaining("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));

            // null queues will default to default names
            p = new QueueNameValidator(new ServiceControlInstanceMetadata())
            {
                instances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
            Assert.That(ex.Message, Is.StringContaining("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));
        }

        [Test]
        public void DuplicateQueueNamesAreAllowedOnDifferentTransports_ShouldNotThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                TransportPackage = "RabbitMQ",
                AuditLogQueue = "auditlog",
                ErrorLogQueue = "errorlog",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
            Assert.That(ex.Message, Is.StringContaining("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));

            // null queues will default to default names
            p = new QueueNameValidator(new ServiceControlInstanceMetadata())
            {
                instances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
            Assert.That(ex.Message, Is.StringContaining("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));
        }

        [Test]
        public void EnsureDuplicateQueueNamesAreAllowedOnSameTransportWithDifferentConnectionString()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                TransportPackage = "RabbitMQ",
                AuditQueue = "RMQaudit",
                AuditLogQueue = "RMQauditlog",
                ErrorQueue = "RMQerror",
                ErrorLogQueue = "RMQerrorlog",
                ConnectionString = "afakeconnectionstring"
            };

            var p = new QueueNameValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());
            Assert.That(ex.Message, Is.StringContaining("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for"));

            newInstance.ConnectionString = "differentconnectionstring";
            p = new QueueNameValidator(newInstance)
            {
                instances = instances
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreNotTakenByAnotherInstance());

        }
    }
}
