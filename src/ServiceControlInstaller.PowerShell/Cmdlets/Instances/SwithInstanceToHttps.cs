// ReSharper disableUnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ServiceControlInstaller.PowerShell
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using System.Security.Cryptography.X509Certificates;
    using HttpApiWrapper;
    using ServiceControlInstaller.Engine.Instances;
    
    [Cmdlet(VerbsCommon.Switch, "ServiceControlInstanceToHTTPS")]
    public class SwithInstanceToHttps  : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0, HelpMessage = "Specify the ServiceControl instance name to remove")]
        public string Name { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "Thumbprint of the certificate to use")]
        public string Thumbprint { get; set; }

        private X509Certificate2 certificate;

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
                if (instance.Protocol.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWarning($"{Name} is already configured for HTTPS");
                    return;
                }

                var store = new X509Store(StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                certificate = store.Certificates.Find(X509FindType.FindByThumbprint, Thumbprint, false).Cast<X509Certificate2>().FirstOrDefault();
                store.Close();

                if (certificate == null)
                {
                    throw new ItemNotFoundException("A certificate matching the provided thumbprint could not be found in the Local Machine certificate store");
                }
                SslCert.MigrateToHttps(instance.AclUrl, certificate.GetCertHash());
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.InvalidArgument, null));
            }
        }
    }
}
