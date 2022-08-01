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
            instanceA.SetupGet(p => p.ForwardErrorMessages).Returns(true);

            var instanceB = new Mock<IServiceControlInstance>();
            instanceB.SetupGet(p => p.TransportPackage).Returns(ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.RabbitMQClassicConventionalRoutingTopology || t.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology));
            instanceB.SetupGet(p => p.ErrorQueue).Returns(@"RMQerror");
            instanceB.SetupGet(p => p.ErrorLogQueue).Returns(@"RMQerrorlog");
            instanceB.SetupGet(p => p.ForwardErrorMessages).Returns(true);
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
            var existingAudit = new Mock<IServiceControlAuditInstance>();
            existingAudit.SetupGet(p => p.TransportPackage).Returns(ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ));
            existingAudit.SetupGet(p => p.AuditQueue).Returns(@"audit");

            var newInstance = new ServiceControlAuditNewInstance
            {
                TransportPackage = ServiceControlCoreTransports.All.First(t => t.Name == TransportNames.MSMQ),
                AuditQueue = "audit"
            };

            var validator = new QueueNameValidator(newInstance)
            {
                AuditInstances = new List<IServiceControlAuditInstance> { existingAudit.Object }
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

        List<IServiceControlInstance> instances;
    }
}
