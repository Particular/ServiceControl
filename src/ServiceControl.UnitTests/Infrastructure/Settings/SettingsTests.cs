namespace ServiceControl.UnitTests.Infrastructure.Settings
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class SettingsTests
    {
        [Test]
        public void Should_read_RemoteInstances_from_serialized_json()
        {
            var configValue = """[{"api_uri":"http://instance1"},{"api_uri":"http://instance2"}]""";
            var remoteInstances = PrimaryOptionsPostConfiguration.ParseRemoteInstances(configValue);

            Assert.That(
                new[] { new RemoteInstanceSetting("http://instance1"), new RemoteInstanceSetting("http://instance2") },
                Is.EqualTo(remoteInstances).Using((IComparer<RemoteInstanceSetting>)new RemoteInstanceSettingComparer()));
        }

        class RemoteInstanceSettingComparer : Comparer<RemoteInstanceSetting>
        {
            public override int Compare(RemoteInstanceSetting x, RemoteInstanceSetting y)
            {
                return x.BaseAddress.Equals(y.BaseAddress) ? 0 : 1;
            }
        }
    }
}