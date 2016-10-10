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

    [Cmdlet(VerbsCommon.Show, "UrlAclCertificate")]
    public class ShowUrlAclCertificate : PSCmdlet
    {
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The URL to add to the URLACL list. This should always in a trailing /", ValueFromPipelineByPropertyName = true)]
        public string Url { get; set; }

        [Parameter(HelpMessage = "Displays the certificate using the built Windows Certificate UI")]
        public SwitchParameter ShowGui { get; set; }

        private UrlReservation reservation;

        protected override void BeginProcessing()
        {
            Account.TestIfAdmin();
        }

        protected override void ProcessRecord()
        {
            Uri uri;
            try
            {
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
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, null, ErrorCategory.InvalidArgument, null));
                return;
            }

            var certificate = SslCert.GetCertificate(uri.Port);
            if (certificate == null)
                return;
            if (ShowGui.ToBool())
            {
                X509Certificate2UI.DisplayCertificate(certificate);
            }
            else
            {
                WriteObject(certificate);
            }
        }
    }
}
