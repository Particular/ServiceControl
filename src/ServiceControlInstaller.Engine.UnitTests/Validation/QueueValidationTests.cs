namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.Collections.Generic;
    using System.Linq;
    using Engine.Validation;
    using Instances;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class QueueValidationTests
    {
        [SetUp]
        public void Init()
        {
            var instanceA = new Mock<IServiceControlInstance>();
            instanceA.SetupGet(p => p.TransportPackage).Returns(ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ));
            instanceA.SetupGet(p => p.ErrorQueue).Returns(@"error");
            instanceA.SetupGet(p => p.ErrorLogQueue).Returns(@"errorlog");

            var instanceB = new Mock<IServiceControlInstance>();
            instanceB.SetupGet(p => p.TransportPackage).Returns(ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQConventionalRoutingTopology));
            instanceB.SetupGet(p => p.ErrorQueue).Returns(@"RMQerror");
            instanceB.SetupGet(p => p.ErrorLogQueue).Returns(@"RMQerrorlog");
            instanceB.SetupGet(p => p.ConnectionString).Returns(@"afakeconnectionstring");

            instances = new List<IServiceControlInstance>
            {
                instanceA.Object,
                instanceB.Object
            };
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                AuditLogQueue = "auditlog",
                ErrorLogQueue = "errorlog",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = new List<IServiceControlInstance>()
            };
            Assert.DoesNotThrow(() => p.CheckQueueNamesAreUniqueWithinInstance());
        }

        [Test]
        public void CheckQueueNamesAreUniqueShouldThrow()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                AuditLogQueue = "audit",
                ErrorLogQueue = "error",
                AuditQueue = "audit",
                ErrorQueue = "error"
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
                AuditLogQueue = "auditlog2",
                ErrorLogQueue = "errorlog2",
                AuditQueue = "audit2",
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
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                AuditLogQueue = "auditlog",
                ErrorLogQueue = "errorlog",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for ErrorLogQueue, ErrorQueue"));

            // null queues will default to default names
            p = new QueueNameValidator(new ServiceControlNewInstance())
            {
                SCInstances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain("The queue name for ErrorQueue is already assigned to another ServiceControl instance"));
        }

        [Test]
        public void DuplicateQueueNamesAreAllowedOnDifferentTransports_ShouldNotThrow()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQConventionalRoutingTopology),
                AuditLogQueue = "auditlog",
                ErrorLogQueue = "errorlog",
                AuditQueue = "audit",
                ErrorQueue = "error"
            };

            var p = new QueueNameValidator(newInstance)
            {
                SCInstances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for ErrorLogQueue, ErrorQueue"));

            // null queues will default to default names
            p = new QueueNameValidator(new ServiceControlNewInstance())
            {
                SCInstances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckQueueNamesAreNotTakenByAnotherServiceControlInstance());
            Assert.That(ex.Message, Does.Contain("The queue name for ErrorQueue is already assigned to another ServiceControl instance"));
        }

        [Test]
        public void EnsureDuplicateQueueNamesAreAllowedOnSameTransportWithDifferentConnectionString()
        {
            var newInstance = new ServiceControlNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQConventionalRoutingTopology),
                AuditQueue = "RMQaudit",
                AuditLogQueue = "RMQauditlog",
                ErrorQueue = "RMQerror",
                ErrorLogQueue = "RMQerrorlog",
                ConnectionString = "afakeconnectionstring"
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

        List<IServiceControlInstance> instances;
    }
}
