﻿namespace ServiceControl.UnitTests.Infrastructure
{
    using System.Linq;
    using NLog.Config;
    using NLog.Targets;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    public class LoggingConfiguratorTests
    {
        [TearDown]
        public void ResetLogConfiguration()
        {
            NLog.LogManager.Configuration = new LoggingConfiguration();
            LoggingConfigurator.Settings = null;
        }

        [Test]
        public void Should_remove_Console_targets_if_not_printing_to_console()
        {
            LoggingConfigurator.Settings = null;
            var loggingSettings = new LoggingSettings("Test", false);

            LoggingConfigurator.ConfigureLogging(loggingSettings);

            var logConfiguration = NLog.LogManager.Configuration;

            Assert.IsEmpty(logConfiguration.LoggingRules.Where(rule => rule.Targets.Any(target => target is ColoredConsoleTarget)));
        }

        [Test]
        public void Should_contain_Console_targets_if_printing_to_console()
        {
            LoggingConfigurator.Settings = null;
            var loggingSettings = new LoggingSettings("Test", true);

            LoggingConfigurator.ConfigureLogging(loggingSettings);

            var logConfiguration = NLog.LogManager.Configuration;

            Assert.IsNotEmpty(logConfiguration.LoggingRules.Where(rule => rule.Targets.Any(target => target is ColoredConsoleTarget)));
        }
    }
}