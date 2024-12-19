namespace ServiceControl.Management.PowerShell.Validation
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    public class TransportValuesGenerator : IValidateSetValuesGenerator
    {
        public string[] GetValidValues() => ServiceControlCoreTransports.GetSupportedTransports()
            .Select(t => t.Name)
            .ToArray();
    }
}