namespace ServiceControl.UnitTests.Infrastructure.Settings
{
    using System.Collections.Generic;
    using System.Configuration;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class SettingsTests
    {
        [Test]
        public void Should_read_RemoteInstances_from_serialized_json()
        {
            var configValue = "[{'api_uri':'http://instance1'},{'api_uri':'http://instance2'}]'";
            var remoteInstances = Settings.ParseRemoteInstances(configValue);

            CollectionAssert.AreEqual(remoteInstances, new[]
            {
                new RemoteInstanceSetting
                {
                    ApiUri = "http://instance1"
                },
                new RemoteInstanceSetting
                {
                    ApiUri = "http://instance2"
                }
            }, new RemoteInstanceSettingComparer());
        }

        class RemoteInstanceSettingComparer : Comparer<RemoteInstanceSetting>
        {
            public override int Compare(RemoteInstanceSetting x, RemoteInstanceSetting y)
            {
                return x.ApiUri.Equals(y.ApiUri) ? 0 : 1;
            }
        }
    }
}