namespace ServiceControl.MultiInstance.AcceptanceTests.Docker
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class RabbitMqDirect : AcceptanceTests
    {
        [Test]
        public void Verify_rabbitmqdirect_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqdirect_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqdirect_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqdirect_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqdirect_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqdirect_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.direct-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
