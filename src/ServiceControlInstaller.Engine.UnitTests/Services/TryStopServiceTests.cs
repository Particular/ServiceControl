namespace ServiceControlInstaller.Engine.UnitTests.Services
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using System.Threading.Tasks;
    using Engine.Services;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class TryStopServiceTests
    {
        string installPath;

        [SetUp]
        public void SetUp()
        {
            installPath = Path.Combine(Path.GetTempPath(), "TryStopServiceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installPath);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(installPath, true);

        [Test]
        public async Task Waits_for_files_released_more_than_five_seconds_after_the_service_stops()
        {
            var lockedDll = new FileStream(Path.Combine(installPath, "Locked.dll"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            var release = Task.Delay(TimeSpan.FromSeconds(7)).ContinueWith(_ => lockedDll.Dispose());

            var instance = new ServiceControlInstance(new StoppedServiceController(Path.Combine(installPath, "ServiceControl.exe")));

            Assert.That(instance.TryStopService(), Is.True);

            await release;
        }

        class StoppedServiceController(string exePath) : IWindowsServiceController
        {
            public string ServiceName => "ServiceControl";
            public string ExePath { get; } = exePath;
            public ServiceControllerStatus Status => ServiceControllerStatus.Running;
            public string Account => "system";
            public string DisplayName => throw new NotImplementedException();
            public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public bool Exists() => true;
            public void Refresh() { }
            public void Stop() { }
            public void WaitForStatus(ServiceControllerStatus stopped, TimeSpan timeSpan) { }
            public void Start() => throw new NotImplementedException();
            public void Delete() => throw new NotImplementedException();
            public void SetStartupMode(string v) => throw new NotImplementedException();
            public void ChangeAccountDetails(string accountName, string serviceAccountPwd) => throw new NotImplementedException();
        }
    }
}
