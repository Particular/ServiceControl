// ReSharper disableUnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using HttpApiWrapper;
    using ServiceControlInstaller.Engine.Instances;


    [Cmdlet(VerbsCommon.Switch, "ServiceControlInstanceToHTTP")]
    public class SwitchInstanceToHttp : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl instance name to remove")]
        public string Name { get; set; }


        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            try
            {
                var instance = ServiceControlInstance.FindByName(Name);
                if (instance == null)
                {
                    throw new ItemNotFoundException("An instance called {Name} was not found");
                }
                if (instance.Protocol.Equals("http", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWarning($"{Name} is already configured for HTTP");
                    return;
                }

                SslCert.MigrateToHttp(instance.AclUrl);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.InvalidArgument, null));
            }
        }
    }
}
