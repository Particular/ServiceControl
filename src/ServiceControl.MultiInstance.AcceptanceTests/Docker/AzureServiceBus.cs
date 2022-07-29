namespace ServiceControl.MultiInstance.AcceptanceTests.Docker
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class AzureServiceBus : AcceptanceTests
    {
        [Test]
        public void Verify_azureservicebus_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azureservicebus_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azureservicebus_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azureservicebus_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azureservicebus_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azureservicebus_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azureservicebus-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
