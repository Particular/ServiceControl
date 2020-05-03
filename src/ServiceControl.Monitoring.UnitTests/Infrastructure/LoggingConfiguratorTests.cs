namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System.Linq;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NUnit.Framework;

    [TestFixture]
    public class LoggingConfiguratorTests
    {
        [TearDown]
        public void ResetLogConfiguration()
        {
            NLog.LogManager.Configuration = new LoggingConfiguration();
        }

        [Test]
        public void Should_remove_Console_targets_if_not_printing_to_console()
        {
            var settings = new Settings
            {
                LogLevel = LogLevel.Info,
                LogPath = "TestLogPath"
            };

            MonitorLogs.Configure(settings, false);

            var logConfiguration = NLog.LogManager.Configuration;

            Assert.IsEmpty(logConfiguration.LoggingRules.Where(rule => rule.Targets.Any(target => target is ColoredConsoleTarget)));
        }

        [Test]
        public void Should_contain_Console_targets_if_printing_to_console()
        {
            var settings = new Settings
            {
                LogLevel = LogLevel.Info,
                LogPath = "TestLogPath"
            };

            MonitorLogs.Configure(settings, true);

            var logConfiguration = NLog.LogManager.Configuration;

            Assert.IsNotEmpty(logConfiguration.LoggingRules.Where(rule => rule.Targets.Any(target => target is ColoredConsoleTarget)));
        }
    }
}