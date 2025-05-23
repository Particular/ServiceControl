namespace ServiceControl.Management.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlTransportTypes")]
    public class GetServiceControlTransportTypes : Cmdlet
    {
        protected override void ProcessRecord()
        {
            var transportInfos = ServiceControlCoreTransports.GetSupportedTransports()
                .Select(PsTransportInfo.FromTransport);

            WriteObject(transportInfos, true);
        }
    }

    public class GetSupportedErrorQueueDiscoveryMethods : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject()
            var methods = InstanceFinder.ServiceControlInstances()
                .SelectMany(instance => instance.GetErrorQueueDiscoveryMethods())
                .Distinct()
                .ToList();

            WriteObject(methods, true);
        }
    }
}