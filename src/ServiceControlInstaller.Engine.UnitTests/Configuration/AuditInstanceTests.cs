namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

    [TestFixture]
    class AuditInstanceTests
    {
        [Test]
        public void Should_default_to_raven35_when_no_config_entry_exists()
        {
            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController());

            instance.Reload();

            StringAssert.EndsWith("RavenDb", instance.PersistencePackage.TypeName);
        }

        class FakeWindowsServiceController : IWindowsServiceController
        {
            public string ServiceName => throw new NotImplementedException();

            public string ExePath => "some-path";

            public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public ServiceControllerStatus Status => throw new NotImplementedException();

            public string Account => "system";

            public string DisplayName => throw new NotImplementedException();

            public void ChangeAccountDetails(string accountName, string serviceAccountPwd) => throw new NotImplementedException();
            public void Delete() => throw new NotImplementedException();
            public void Refresh()
            { }
            public void SetStartupMode(string v) => throw new NotImplementedException();
            public void Start() => throw new NotImplementedException();
            public void Stop() => throw new NotImplementedException();
            public void WaitForStatus(ServiceControllerStatus stopped, TimeSpan timeSpan) => throw new NotImplementedException();
        }
    }
}
