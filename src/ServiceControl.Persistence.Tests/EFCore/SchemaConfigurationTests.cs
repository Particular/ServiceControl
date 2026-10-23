namespace ServiceControl.Persistence.Tests;

using System;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
[NonParallelizable]
class SchemaConfigurationTests
{
    const string SchemaVariable = "SERVICECONTROL_DATABASE_SCHEMA";

    static readonly string[] Variables =
    [
        "SERVICECONTROL_DATABASE_CONNECTIONSTRING",
        "SERVICECONTROL_ERRORRETENTIONPERIOD",
        "SERVICECONTROL_MESSAGEBODY_STORAGETYPE",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH",
        SchemaVariable
    ];

    [SetUp]
    public void SetUp()
    {
        TearDown();
        Environment.SetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", "Server=nowhere");
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", "/var/bodies");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var variable in Variables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Absent_or_blank_schema_falls_back_to_sc_primary(string configured)
    {
        Environment.SetEnvironmentVariable(SchemaVariable, configured);

        Assert.That(CreateSettings().Schema, Is.EqualTo("sc_primary"));
    }

    [TestCase("legacy")]
    [TestCase("dbo")]
    [TestCase("public")]
    [TestCase("psw_sc")]
    public void Configured_schema_is_used(string schema)
    {
        Environment.SetEnvironmentVariable(SchemaVariable, schema);

        Assert.That(CreateSettings().Schema, Is.EqualTo(schema));
    }

    static EFPersisterSettings CreateSettings() =>
        (EFPersisterSettings)new TestPersistenceConfiguration().CreateSettings(new SettingsRootNamespace("ServiceControl"));

    sealed class TestPersistenceConfiguration : EFPersistenceConfigurationBase
    {
        public override IPersistence Create(PersistenceSettings settings) => throw new NotSupportedException();

        protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
            new TestPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
    }

    sealed class TestPersisterSettings : EFPersisterSettings;
}
