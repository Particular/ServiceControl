namespace ServiceControl.Docker.AcceptanceTests
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;

    public class SQLServer : AcceptanceTests
    {
        [Test]
        public void Verify_sqlserver_audit_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.sqlserver.audit.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_sqlserver_audit_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.sqlserver.audit-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_sqlserver_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.sqlserver.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_sqlserver_monitoring_init_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.sqlserver.monitoring.init-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_sqlserver_monitoring_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(DockerFolder, "servicecontrol.sqlserver.monitoring-windows.dockerfile"));
            Approver.Verify(readFile);
        }

        [Test]
        public void Verify_sqlserver_windows()
        {
            var readFile = File.ReadAllText(Path.Combine(AcceptanceTests.DockerFolder, "servicecontrol.sqlserver-windows.dockerfile"));
            Approver.Verify(readFile);
        }
    }
}
