namespace ServiceControl.MultiInstance.AcceptanceTests.Docker
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class RabbitMqConventional : AcceptanceTests
    {
        [Test]
        public void Verify_rabbitmqconventional_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqconventional_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqconventional_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqconventional_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqconventional_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqconventional_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.conventional-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
