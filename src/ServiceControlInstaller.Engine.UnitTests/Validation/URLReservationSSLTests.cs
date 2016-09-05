namespace ServiceControlInstaller.Engine.UnitTests.UrlAcl
{
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using HttpApiWrapper;
    using NUnit.Framework;

    public class URLReservationSSLTests
    {
        const string sslurl = "https://bogushostname:12346/";
        const string notsslurl = "http://bogushostname:12346/";

        [Test, Explicit]
        public void AddSSLCertToUrlAclViaGUI()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var selectedCertificate = X509Certificate2UI.SelectFromCollection(store.Certificates, "Local Machine Certificate Store", "Select the SSL Certificate to use", X509SelectionFlag.SingleSelection);
            if (selectedCertificate.Count == 1)
            {
                var reservation = new UrlReservation(sslurl, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
                reservation.Create();
                SslCert.ApplyCertificate(reservation.Port, selectedCertificate[0].GetCertHash());
            }
            store.Close();
        }

        [Test, Explicit]
        public void AddSSLCertToUrlAclViaConsole()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var selectedCertificate = store.Certificates.Find(X509FindType.FindByThumbprint, "A5DA5F7132EEEB80F0719CCC1B3E498774041714", true);
            if (selectedCertificate.Count == 1)
            {
                var reservation = new UrlReservation(sslurl, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
                reservation.Create();
                SslCert.ApplyCertificate(reservation.Port, selectedCertificate[0].GetCertHash());
            }
            store.Close();
        }

        [Test, Explicit]
        public void ClearCertificate()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var selectedCertificate = store.Certificates.Find(X509FindType.FindByThumbprint, "A5DA5F7132EEEB80F0719CCC1B3E498774041714", true);
            if (selectedCertificate.Count == 1)
            {
                var reservation = new UrlReservation(sslurl, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
                reservation.Create();
                SslCert.ApplyCertificate(reservation.Port, selectedCertificate[0].GetCertHash());
                SslCert.ClearCertificate(reservation.Port);
                Assert.IsNull(SslCert.GetCertificate(reservation.Port));
            }
            store.Close();
        }
    }
}