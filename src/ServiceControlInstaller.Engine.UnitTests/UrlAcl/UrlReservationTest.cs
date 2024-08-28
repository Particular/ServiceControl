namespace ServiceControlInstaller.Engine.UnitTests.UrlAcl
{
    using System;
    using System.Linq;
    using System.Security.Principal;
    using Engine.UrlAcl;
    using NUnit.Framework;

    [TestFixture]
    public class UrlReservationTests
    {
        [Test]
        public void GetAllResults()
        {
            var reservations = UrlReservation.GetAll();
            Assert.That(reservations.Count > 0, Is.True, "No UrlAcls found");
            Assert.That(reservations.All(p => !string.IsNullOrWhiteSpace(p.Url)), Is.True, "UrlAcls found with empty URLs");
            Assert.That(reservations.All(p => p.Users.Count > 0), Is.True, "UrlAcls found with empty delegations");

            foreach (var x in reservations)
            {
                Console.Write(x.Url);
                Console.WriteLine(string.Join(", ", x.Users));
            }
        }

        [Test]
        public void ThrowIfCreateInvalidUrlAcl()
        {
            //Try Adding a UrlAcl without a SecurityIdentifier
            var reservation = new UrlReservation(url);
            if (UrlReservation.GetAll().Any(p => p.Url.Equals(reservation.Url, StringComparison.OrdinalIgnoreCase)))
            {
                UrlReservation.Delete(reservation);
                Assert.That(UrlReservation.GetAll().Any(p => p.Url.Equals(reservation.Url, StringComparison.OrdinalIgnoreCase)), Is.False, "UrlAcl exists after deletion");
            }

            Assert.Throws<Exception>(() => UrlReservation.Create(reservation), "UrlAcl incorrectly created with empty delegation");
        }

        [Test]
        public void AddAndDeleteUrlAcl()
        {
            var reservation = new UrlReservation(url, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            reservation.Create();
            Assert.That(UrlReservation.GetAll().Any(p => p.Url.Equals(url)), Is.True, "UrlAcl doesn't exist after creation");
            reservation.Delete();
            Assert.That(UrlReservation.GetAll().Count(p => p.Url.Equals(url)) == 0, Is.True, "UrlAcl exists after deletion");
        }

        [Test]
        public void ClobberUrlAcl()
        {
            //Make Reservation
            foreach (var r in UrlReservation.GetAll().Where(p => p.Url.Equals(url)))
            {
                r.Delete();
            }

            var reservation = new UrlReservation(url, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            reservation.Create();

            //Overwrite Reservation
            var newSid = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var account = (NTAccount)newSid.Translate(typeof(NTAccount));
            var reservation2 = new UrlReservation(url, newSid);
            reservation2.Create();

            var actual = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.That(actual.Users.Count == 1, Is.True, "user count is incorrect");
            Assert.That(actual.Users.Contains(account.Value, StringComparer.OrdinalIgnoreCase), Is.True, "wrong user found");
        }

        [Test]
        public void AddUsersToUrlAcl()
        {
            var reservation = new UrlReservation(url, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            reservation.Create();

            // Read Back the URL
            reservation = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.That(reservation.Users.Count, Is.EqualTo(1), "User count is not 1");
            Assert.That(reservation.Users.First().Equals("Everyone", StringComparison.OrdinalIgnoreCase), Is.True, "User is not 'Everyone'");

            var newAccountSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            reservation.AddSecurityIdentifier(newAccountSid);
            reservation.Create();

            var account = (NTAccount)newAccountSid.Translate(typeof(NTAccount));
            reservation = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.That(reservation.Users.Count, Is.EqualTo(2), "User count is not 2");
            Assert.That(reservation.Users.Contains(account.Value, StringComparer.OrdinalIgnoreCase), Is.True, "Added User not found");
        }

        [Test]
        public void CheckPatternMatching()
        {
            var testUrl = new UrlReservation("http://localhost/");
            Assert.That(testUrl.HTTPS, Is.False);
            Assert.That(testUrl.HostName == "localhost", Is.True);
            Assert.That(testUrl.Port == 80, Is.True);
            Assert.That(testUrl.VirtualDirectory == string.Empty, Is.True);

            testUrl = new UrlReservation("https://localhost:8000/");
            Assert.That(testUrl.HTTPS, Is.True);
            Assert.That(testUrl.HostName == "localhost", Is.True);
            Assert.That(testUrl.Port == 8000, Is.True);
            Assert.That(testUrl.VirtualDirectory == string.Empty, Is.True);

            testUrl = new UrlReservation("https://localhost:8000/foo/api/");
            Assert.That(testUrl.HTTPS, Is.True);
            Assert.That(testUrl.HostName == "localhost", Is.True);
            Assert.That(testUrl.Port == 8000, Is.True);
            Assert.That(testUrl.VirtualDirectory == "foo/api", Is.True);

            testUrl = new UrlReservation("https://localhost/foo/api/");
            Assert.That(testUrl.HTTPS, Is.True);
            Assert.That(testUrl.HostName == "localhost", Is.True);
            Assert.That(testUrl.Port == 443, Is.True);
            Assert.That(testUrl.VirtualDirectory == "foo/api", Is.True);

            testUrl = new UrlReservation("https://[::1]:10253/");
            Assert.That(testUrl.HTTPS, Is.True);
            Assert.That(testUrl.HostName == "[::1]", Is.True);
            Assert.That(testUrl.Port == 10253, Is.True);

            Assert.Throws<ArgumentException>(() => new UrlReservation("https://localhost:8000/foo/api"), "UrlAcl is invalid without trailing /");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var urlReservation in UrlReservation.GetAll().Where(p => p.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            {
                urlReservation.Delete();
            }
        }

        const string url = "http://bogushostname:12345/";
    }
}