namespace ServiceControlInstaller.Engine.UnitTests.Services
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

    [TestFixture]
    public class ServiceControllerExTests
    {
        [Test]
        public void KnownServiceFoundByExe()
        {
            // ReSharper disable once StringLiteralTypo
            var allServiceHostInstances = WindowsServiceController.FindInstancesByExe(@"svchost.exe").ToList();
            Assert.IsTrue(allServiceHostInstances.Count > 0);
            Assert.IsTrue(allServiceHostInstances.All(p => p.ServiceName != null));
            Assert.IsTrue(allServiceHostInstances.All(p => File.Exists(p.ExePath)));
        }

        [Explicit,Test]
        public void CreateAService()
        {
            var s = new WindowsServiceDetails
            {
                ImagePath = @"C:\Program Files (x86)\Particular Software\ServiceControl\ServiceControl.exe  --serviceName=Test.SC",
                DisplayName = "Test SC",
                Name = "Test.SC",
                ServiceAccount = @"NT Authority\NetworkService",
                ServiceAccountPwd = null
            };

            var existing = ServiceControlInstance.Instances().FirstOrDefault(p => p.Name == s.Name);
            if (existing != null)
            {
                ServiceControlInstance.Instances().First(p => p.Name == s.Name).Service.Delete();
            }
            WindowsServiceController.RegisterNewService(s);
            ServiceControlInstance.Instances().First(p => p.Name == s.Name).Service.Delete();
        }   
    }
}
