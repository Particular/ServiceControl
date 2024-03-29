﻿namespace ServiceControl.Management.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Instances;

    [Cmdlet(VerbsCommon.Get, "ServiceControlInstances")]
    public class GetServiceControlInstances : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(InstanceFinder.ServiceControlInstances().Select(PsServiceControl.FromInstance), true);
        }
    }
}