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

    [Cmdlet(VerbsData.Update, "UrlAclCertificate")]
    public class UpdateUrlAclCertificate : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The URL to add to the URLACL list. This should always in a trailing /", ValueFromPipelineByPropertyName = true)]
        public string Url { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "Thumbprint of the certificate to use", ValueFromPipelineByPropertyName = true)]
        public string Thumbprint { get; set; }

        private UrlReservation reservation;
        private X509Certificate certificate;

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            try
            {
                Uri uri;

                if (!Uri.TryCreate(Url, UriKind.Absolute, out uri))
                {
                    throw new ArgumentException("Invalid URL");
                }

                if (uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException("URL must be HTTPS");
                }

                if (!uri.AbsoluteUri.EndsWith("/"))
                {
                    Url = uri.AbsolutePath + "/";
                }

                reservation = UrlReservation.GetAll().FirstOrDefault(p => p.Url.Equals(Url, StringComparison.InvariantCultureIgnoreCase));
                if (reservation == null)
                {
                    throw new ItemNotFoundException("A URLACL reservation matching the provided URL could not be found");
                }

                var store = new X509Store(StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                certificate = store.Certificates.Find(X509FindType.FindByThumbprint, Thumbprint, true).Cast<X509Certificate>().FirstOrDefault();
                store.Close();

                if (certificate == null)
                {
                    throw new ItemNotFoundException("A certificate matching the provided thumbprint could not be found in the Local Machine certificate store");
                }
                SslCert.ApplyCertificate(uri.Port, certificate.GetCertHash());
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.InvalidArgument, null));
            }
        }
    }
}
