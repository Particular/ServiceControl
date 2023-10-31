namespace ServiceControl.UnitTests.API
{
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void TransportNames()
        {
            // These values constitute a public API
            // ServiceControl instances load transports either by Name (preferred) or a type name which may appear in an Alias
            // PowerShell scripts in ServiceControl v5 prefer loading transport by Name, but ServiceControl v4 used the display name,
            // and older display names are represented in the Aliases collection

            var toVerify = ServiceControlCoreTransports.GetAllTransports()
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Name,
                    t.DisplayName,
                    t.Aliases
                })
                .ToArray();

            Approver.Verify(toVerify);
        }
    }
}