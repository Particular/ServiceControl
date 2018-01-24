namespace ServiceControl.UnitTests.Infrastructure.Settings
{
    using System.Collections.Generic;
    using System.Configuration;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class SettingsTests
    {
        [SetUp]
        public void WriteSetting()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Clear();
            // Remote instances
            config.AppSettings.Settings.Add("ServiceControl/RemoteInstances", "[{'ApiUri':'http://instance1', 'QueueAddress':'instance1@pc1'},{'ApiUri':'http://instance2', 'QueueAddress':'instance1@pc2'}]'");
            // Various mandatory settings
            config.AppSettings.Settings.Add("ServiceControl/ForwardAuditMessages", "false");
            config.AppSettings.Settings.Add("ServiceControl/ForwardErrorMessages", "false");
            config.AppSettings.Settings.Add("ServiceControl/AuditRetentionPeriod", 1.ToString());
            config.AppSettings.Settings.Add("ServiceControl/ErrorRetentionPeriod", 10.ToString());
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        [Test]
        public void Should_read_RemoteInstances_from_serialized_json()
        {
            var settings = new Settings();
            var remoteInstances = settings.RemoteInstances;
            CollectionAssert.AreEqual(remoteInstances, new List<RemoteInstanceSetting>
            {
                new RemoteInstanceSetting { ApiUri = "http://instance1", QueueAddress = "instance1@pc1"},
                new RemoteInstanceSetting { ApiUri = "http://instance2", QueueAddress = "instance1@pc2"}
            }, new RemoteInstanceSettingComparer());
        }

        class RemoteInstanceSettingComparer : Comparer<RemoteInstanceSetting>
        {
            public override int Compare(RemoteInstanceSetting x, RemoteInstanceSetting y)
            {
                return x.QueueAddress.Equals(y.QueueAddress) && x.ApiUri.Equals(y.ApiUri) ? 0 : 1;
            }
        }
    }
}