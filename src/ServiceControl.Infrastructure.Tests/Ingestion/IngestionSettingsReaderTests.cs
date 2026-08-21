namespace ServiceControl.Infrastructure.Tests.Ingestion;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure.Ingestion;
using ServiceControl.Infrastructure.Tests.Auth;

// The setting under test is one process-wide environment variable, so these cannot run beside
// each other: one test's teardown clears what another just set.
[TestFixture]
[NonParallelizable]
class IngestionSettingsReaderTests
{
    [TearDown]
    public void ClearSetting() => Environment.SetEnvironmentVariable(EnvironmentVariable, null);

    [Test]
    public void An_unconfigured_setting_leaves_the_choice_to_the_caller()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(IngestionSettingsReader.ReadBatchSize(Root, SettingName, validateConfiguration: true), Is.Null);
            Assert.That(IngestionSettingsReader.ReadMaxParallelWriters(Root, SettingName, validateConfiguration: true), Is.Null);
            Assert.That(IngestionSettingsReader.ReadBatchTimeout(Root, SettingName, validateConfiguration: true), Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public void A_configured_setting_is_read()
    {
        Configure("250");

        Assert.That(IngestionSettingsReader.ReadBatchSize(Root, SettingName, validateConfiguration: true), Is.EqualTo(250));
    }

    [Test]
    public void A_configured_timeout_is_read()
    {
        Configure("00:00:00.250");

        Assert.That(IngestionSettingsReader.ReadBatchTimeout(Root, SettingName, validateConfiguration: true), Is.EqualTo(TimeSpan.FromMilliseconds(250)));
    }

    [TestCase("0")]
    [TestCase("1001")]
    public void A_batch_size_outside_the_range_is_rejected(string value)
    {
        Configure(value);

        Assert.That(() => IngestionSettingsReader.ReadBatchSize(Root, SettingName, validateConfiguration: true), Throws.Exception.With.Message.Contains(SettingName));
    }

    [TestCase("0")]
    [TestCase("17")]
    public void A_writer_count_outside_the_range_is_rejected(string value)
    {
        Configure(value);

        Assert.That(() => IngestionSettingsReader.ReadMaxParallelWriters(Root, SettingName, validateConfiguration: true), Throws.Exception.With.Message.Contains(SettingName));
    }

    [TestCase("-00:00:01")]
    [TestCase("00:00:06")]
    public void A_timeout_outside_the_range_is_rejected(string value)
    {
        Configure(value);

        Assert.That(() => IngestionSettingsReader.ReadBatchTimeout(Root, SettingName, validateConfiguration: true), Throws.Exception.With.Message.Contains(SettingName));
    }

    [Test]
    public void A_timeout_that_is_not_a_TimeSpan_is_rejected_even_without_validation()
    {
        Configure("soon");

        Assert.That(() => IngestionSettingsReader.ReadBatchTimeout(Root, SettingName, validateConfiguration: false), Throws.Exception.With.Message.Contains(SettingName));
    }

    [Test]
    public void An_out_of_range_value_is_taken_as_it_stands_when_validation_is_off()
    {
        Configure("5000");

        Assert.That(IngestionSettingsReader.ReadBatchSize(Root, SettingName, validateConfiguration: false), Is.EqualTo(5000));
    }

    [Test]
    public void A_storage_that_takes_concurrent_batches_gets_the_default_when_nothing_is_configured() =>
        Assert.That(
            IngestionSettingsReader.ResolveMaxParallelWriters(null, storageSupportsConcurrentBatches: true, SettingName, NullLogger.Instance),
            Is.EqualTo(IngestionSettingsReader.DefaultMaxParallelWriters));

    [Test]
    public void A_storage_that_takes_concurrent_batches_gets_what_is_configured() =>
        Assert.That(
            IngestionSettingsReader.ResolveMaxParallelWriters(7, storageSupportsConcurrentBatches: true, SettingName, NullLogger.Instance),
            Is.EqualTo(7));

    [Test]
    public void A_storage_that_does_not_take_concurrent_batches_is_held_at_one_quietly()
    {
        using var recorder = new RecordingLoggerProvider();

        var writers = IngestionSettingsReader.ResolveMaxParallelWriters(null, storageSupportsConcurrentBatches: false, SettingName, recorder.CreateLogger(SettingName));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writers, Is.EqualTo(1));
            Assert.That(recorder.Entries, Is.Empty, "a default that was never asked for is not worth warning about");
        }
    }

    [Test]
    public void A_storage_that_does_not_take_concurrent_batches_says_so_when_it_overrules_a_setting()
    {
        using var recorder = new RecordingLoggerProvider();

        var writers = IngestionSettingsReader.ResolveMaxParallelWriters(4, storageSupportsConcurrentBatches: false, SettingName, recorder.CreateLogger(SettingName));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writers, Is.EqualTo(1));
            Assert.That(recorder.Entries.Select(entry => entry.Level), Is.EqualTo(new[] { LogLevel.Warning }));
        }
    }

    static void Configure(string value) => Environment.SetEnvironmentVariable(EnvironmentVariable, value);

    const string SettingName = "IngestionBatchSetting";
    static readonly SettingsRootNamespace Root = new("IngestionSettingsReaderTests");
    static readonly string EnvironmentVariable = $"{Root}_{SettingName}".ToUpperInvariant();
}