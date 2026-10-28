namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.EFCore.Abstractions;

// Covers the settings-reader validation for the query time limit applied to message view queries.
[TestFixture]
[NonParallelizable]
class QueryTimeoutConfigurationTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    const string QueryTimeoutVariable = "SERVICECONTROL_QUERYTIMEOUTINSECONDS";

    static readonly string[] Keys =
    [
        QueryTimeoutVariable,
        "SERVICECONTROL_DATABASE_CONNECTIONSTRING",
        "SERVICECONTROL_ERRORRETENTIONPERIOD",
        "SERVICECONTROL_MESSAGEBODY_STORAGETYPE",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH"
    ];

    [SetUp]
    public void SetUp()
    {
        ClearKeys();
        Environment.SetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", "Server=nowhere");
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");
    }

    [TearDown]
    public void TearDown() => ClearKeys();

    static void ClearKeys()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Test]
    public void Defaults_to_one_minute()
    {
        var settings = CreateSettings();

        Assert.That(settings.QueryTimeout, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Reads_the_configured_value()
    {
        Environment.SetEnvironmentVariable(QueryTimeoutVariable, "120");

        var settings = CreateSettings();

        Assert.That(settings.QueryTimeout, Is.EqualTo(TimeSpan.FromSeconds(120)));
    }

    [TestCase("0")]
    [TestCase("-5")]
    [TestCase("3700")]
    public void Falls_back_to_the_default_for_values_outside_the_allowed_range(string value)
    {
        Environment.SetEnvironmentVariable(QueryTimeoutVariable, value);

        var settings = CreateSettings();

        Assert.That(settings.QueryTimeout, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    static EFPersisterSettings CreateSettings() =>
        (EFPersisterSettings)new TestPersistenceConfiguration().CreateSettings(TestNamespace);

    sealed class TestPersistenceConfiguration : EFPersistenceConfigurationBase
    {
        public override IPersistence Create(PersistenceSettings settings) => throw new NotSupportedException();

        protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
            new TestPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
    }

    sealed class TestPersisterSettings : EFPersisterSettings;
}
