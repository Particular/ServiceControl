namespace ServiceControlInstaller.Engine.UnitTests.Services
{
    using System.IO;
    using System.Linq;
    using Engine.Services;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class ServiceControllerExTests
    {
        [Test]
        public void KnownServiceFoundByExe()
        {
            var allServiceHostInstances = WindowsServiceController.FindInstancesByExe(@"svchost.exe").ToList();
            Assert.That(allServiceHostInstances.Count, Is.GreaterThan(0));
            Assert.That(allServiceHostInstances.All(p => File.Exists(p.ExePath)), Is.True);
        }

        [Test]
        [Explicit]
        public void CreateAService()
        {
            var s = new WindowsServiceDetails
            {
                ImagePath = @"C:\Program Files (x86)\Particular Software\ServiceControl\ServiceControl.exe",
                DisplayName = "Test SC",
                Name = "Test.SC",
                ServiceAccount = @"NT Authority\NetworkService",
                ServiceAccountPwd = null
            };

            var existing = InstanceFinder.ServiceControlInstances().FirstOrDefault(p => p.Name == s.Name);
            if (existing != null)
            {
                InstanceFinder.ServiceControlInstances().First(p => p.Name == s.Name).Service.Delete();
            }

            WindowsServiceController.RegisterNewService(s);
            InstanceFinder.ServiceControlInstances().First(p => p.Name == s.Name).Service.Delete();
        }
    }
}