namespace ServiceControl.UnitTests.Infrastructure
{
    using System.Linq;
    using NUnit.Framework;
    using Particular.ServiceControl;

    [TestFixture]
    public class PrimaryAssemblyBoundaryTests
    {
        [Test]
        public void The_primary_does_not_reference_the_standalone_audit_executable()
        {
            var referenced = typeof(HostingComponent).Assembly.GetReferencedAssemblies().Select(name => name.Name);

            Assert.That(referenced, Does.Not.Contain("ServiceControl.Audit"),
                "ServiceControl.Audit is a standalone composition root holding RavenDB persistence selection, standalone "
                + "settings, API hosting, installer commands and its own NServiceBus endpoint. The primary owns a copy of "
                + "the audit runtime instead, so that project stays off its reference graph.");
        }
    }
}
