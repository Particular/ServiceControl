namespace ServiceControl.Management.PowerShell.Validation
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    public class PrimaryPersistenceTypeValuesGenerator : IValidateSetValuesGenerator
    {
        public string[] GetValidValues() => ServiceControlPersisters.GetSupportedPrimaryManifests()
            .Select(m => m.Name)
            .ToArray();
    }

    public class AuditPersistenceTypeValuesGenerator : IValidateSetValuesGenerator
    {
        public string[] GetValidValues() => ServiceControlPersisters.GetSupportedAuditManifests()
            .Select(m => m.Name)
            .ToArray();
    }
}
