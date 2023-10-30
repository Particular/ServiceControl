namespace ServiceControl.Management.PowerShell.Validation
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    public class ValidateTransportAttribute : ValidateArgumentsAttribute
    {
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            var transportType = (string)arguments;

            if (!ServiceControlCoreTransports.GetTransportNames(true).Contains(transportType))
            {
                var codenamesOnly = ServiceControlCoreTransports.GetTransportNames(false)
                    .OrderBy(name => name);

                var okNamesString = string.Join(",", codenamesOnly);

                throw new ValidationMetadataException($"The argument \"{transportType}\" does not belong to the list \"{okNamesString}\". Supply a transport type from the list and try the command again.");
            }
        }
    }
}
