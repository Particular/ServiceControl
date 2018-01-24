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
            config.AppSettings.Settings.Add("ServiceControl/RemoteInstances", "[{'Uri':'http://instance1', 'Address':'instance1@pc1'},{'Uri':'http://instance2', 'Address':'instance1@pc2'}]'");
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
            CollectionAssert.AreEqual(remoteInstances, new List<Settings.RemoteInstanceSetting>
            {
                new Settings.RemoteInstanceSetting { Uri = "http://instance1", Address = "instance1@pc1"},
                new Settings.RemoteInstanceSetting { Uri = "http://instance2", Address = "instance1@pc2"}
            }, new RemoteInstanceSettingComparer());
        }

        class RemoteInstanceSettingComparer : Comparer<Settings.RemoteInstanceSetting>
        {
            public override int Compare(Settings.RemoteInstanceSetting x, Settings.RemoteInstanceSetting y)
            {
                return x.Address.Equals(y.Address) && x.Uri.Equals(y.Uri) ? 0 : 1;
            }
        }
    }
}