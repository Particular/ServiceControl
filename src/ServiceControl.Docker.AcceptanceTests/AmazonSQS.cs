namespace ServiceControl.Docker.AcceptanceTests
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class AmazonSQS : AcceptanceTests
    {
        [Test]
        public void Verify_amazonsqs_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_amazonsqs_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_amazonsqs_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_amazonsqs_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_amazonsqs_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_amazonsqs_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.amazonsqs-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
