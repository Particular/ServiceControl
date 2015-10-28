// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Management.Automation;
    using ServiceControlInstaller.Engine.Ports;

    [Cmdlet(VerbsDiagnostic.Test, "IfPortIsAvailable")]
    public class TestIfPortIsAvailable : PSCmdlet
    {
        [ValidateRange(1, 65535)]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the port number to test")]
        public int[] Port  { get; set; }
        
        protected override void ProcessRecord()
        {
            foreach (var port in Port)
            {
                try
                {
                    WriteObject(new PsPortAvailability
                    {
                        Port = port,
                        Available = PortUtils.CheckAvailable(port)
                    });
                }
                catch (Exception ex)
                {
                    WriteError(new ErrorRecord(ex, null, ErrorCategory.InvalidOperation, port));
                }
            }       
        }
    }
}