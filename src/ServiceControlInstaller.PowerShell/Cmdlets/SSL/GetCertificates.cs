// ReSharper disableUnusedMember.Global
namespace ServiceControlInstaller.PowerShell
{
    using System.Linq;
    using System.Management.Automation;
    using System.Security.Cryptography.X509Certificates;

    [Cmdlet(VerbsCommon.Get, "Certificates")]
    public class GetCertificates :PSCmdlet
    {
        protected override void ProcessRecord()
        {
            var store = new X509Store(StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            WriteObject(store.Certificates.Cast<X509Certificate2>(), true);
            store.Close();
        }
    }
}
