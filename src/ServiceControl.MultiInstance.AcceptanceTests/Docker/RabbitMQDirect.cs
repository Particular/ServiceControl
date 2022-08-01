namespace ServiceControl.MultiInstance.AcceptanceTests.Docker
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class RabbitMqDirect : AcceptanceTests
    {
        [Test]
        public void Verify_rabbitmqclassicdirect_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicdirect_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicdirect_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicdirect_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicdirect_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqclassicdirect_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.classic.direct-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_rabbitmqquorumdirect_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.rabbitmq.quorum.direct-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
