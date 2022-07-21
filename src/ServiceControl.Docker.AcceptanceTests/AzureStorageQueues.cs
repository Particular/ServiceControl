namespace ServiceControl.Docker.AcceptanceTests
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class AzureStorageQueues : AcceptanceTests
    {
        [Test]
        public void Verify_azurestoragequeues_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azurestoragequeues_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azurestoragequeues_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azurestoragequeues_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azurestoragequeues_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_azurestoragequeues_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.azurestoragequeues-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
