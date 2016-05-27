namespace ServiceControlInstaller.Engine.UnitTests.UrlAcl
{
    using System;
    using System.Linq;
    using System.Security.Principal;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.UrlAcl;

    [TestFixture]
    public class UrlReservationTests
    {
        const string url = "http://bogushostname:12345/";
    
        [Test]
        public void GetAllResults()
        {
            var reservations = UrlReservation.GetAll();
            Assert.IsTrue(reservations.Count > 0, "No UrlAcls found");
            Assert.IsTrue(reservations.All(p => !string.IsNullOrWhiteSpace(p.Url)), "UrlAcls found with empty URLs");
            Assert.IsTrue(reservations.All(p => p.Users.Count > 0), "UrlAcls found with empty delegations");

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
                 Assert.IsFalse(UrlReservation.GetAll().Any(p => p.Url.Equals(reservation.Url, StringComparison.OrdinalIgnoreCase)), "UrlAcl exists after deletion");                  
            }
            Assert.Throws<Exception>(() => UrlReservation.Create(reservation), "UrlAcl incorrectly created with empty delegation");
        }

        [Test]
        public void AddAndDeleteUrlAcl()
        {
            var reservation = new UrlReservation(url, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            reservation.Create();
            Assert.IsTrue(UrlReservation.GetAll().Any(p => p.Url.Equals(url)), "UrlAcl doesn't exist after creation");
            reservation.Delete();
            Assert.IsTrue(UrlReservation.GetAll().Count(p => p.Url.Equals(url)) == 0, "UrlAcl exists after deletion");
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
            var account = (NTAccount) newSid.Translate(typeof(NTAccount));
            var reservation2 = new UrlReservation(url, newSid);
            reservation2.Create();

            var actual = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.IsTrue(actual.Users.Count == 1, "user count is incorrect ");
            Assert.IsTrue(actual.Users.Contains(account.Value, StringComparer.OrdinalIgnoreCase), "wrong user found");
        }

        [Test]
        public void AddUsersToUrlAcl()
        {
            var reservation = new UrlReservation(url, new SecurityIdentifier(WellKnownSidType.WorldSid, null));
            reservation.Create(); 

            // Read Back the URL 
            reservation = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.IsTrue(reservation.Users.Count == 1, "User count is not 1");
            Assert.IsTrue(reservation.Users.First().Equals("Everyone", StringComparison.OrdinalIgnoreCase), "User is not 'Everyone'");

            var newAccountSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            reservation.AddSecurityIdentifier(newAccountSid);
            reservation.Create();
            
            var account = (NTAccount) newAccountSid.Translate(typeof(NTAccount));
            reservation = UrlReservation.GetAll().First(p => p.Url == reservation.Url);
            Assert.IsTrue(reservation.Users.Count == 2, "User count is not 2");
            Assert.IsTrue(reservation.Users.Contains(account.Value, StringComparer.OrdinalIgnoreCase), "Added User not found");
            
        }

        [Test]
        public void CheckPatternMatching()
        {
            var testUrl = new UrlReservation("http://localhost/");
            Assert.IsFalse(testUrl.HTTPS);
            Assert.IsTrue(testUrl.HostName == "localhost");
            Assert.IsTrue(testUrl.Port == 80);
            Assert.IsTrue(testUrl.VirtualDirectory == String.Empty);

            testUrl = new UrlReservation("https://localhost:8000/");
            Assert.IsTrue(testUrl.HTTPS);
            Assert.IsTrue(testUrl.HostName == "localhost");
            Assert.IsTrue(testUrl.Port == 8000);
            Assert.IsTrue(testUrl.VirtualDirectory == String.Empty);

            testUrl = new UrlReservation("https://localhost:8000/foo/api/");
            Assert.IsTrue(testUrl.HTTPS);
            Assert.IsTrue(testUrl.HostName == "localhost");
            Assert.IsTrue(testUrl.Port == 8000);
            Assert.IsTrue(testUrl.VirtualDirectory == "foo/api");

            testUrl = new UrlReservation("https://localhost/foo/api/");
            Assert.IsTrue(testUrl.HTTPS);
            Assert.IsTrue(testUrl.HostName == "localhost");
            Assert.IsTrue(testUrl.Port == 443);
            Assert.IsTrue(testUrl.VirtualDirectory == "foo/api");


            // ReSharper disable once ObjectCreationAsStatement
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
    }
}
