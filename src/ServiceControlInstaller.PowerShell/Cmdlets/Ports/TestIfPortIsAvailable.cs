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

                bool available;
                try
                {
                    PortUtils.CheckAvailable(port);
                    available = true;
                }
                catch (Exception)
                {
                    available = false;
                }

                var p = new PSObject
                {
                    Properties =
                    {
                        new PSNoteProperty("Port", port),
                        new PSNoteProperty("Available", available)
                    },
                    TypeNames = { "PortAvailability.Information" }
                };
                WriteObject(p);
            }       
        }
    }
}