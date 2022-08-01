namespace ServiceControl.MultiInstance.AcceptanceTests.Docker
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class RabbitMqConventional : AcceptanceTests
    {
        [Test]
        public void Verify_rabbitmqclassicconventional_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicconventional_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicconventional_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicconventional_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicconventional_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicconventional_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.conventional-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumconventional_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.conventional-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
