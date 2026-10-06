namespace Particular.ServiceControl;

using System;
using System.Collections.Generic;
using System.Globalization;
using global::ServiceControl.Configuration;
using Particular.LicensingComponent.Contracts;
using ServiceBus.Management.Infrastructure.Settings;
using static Particular.LicensingComponent.Contracts.EnvironmentDatum;

class ErrorInstanceTuningEnvironmentDataProvider(Settings settings) : IEnvironmentDataProvider
{
    public IEnumerable<EnvironmentDatum> GetData() =>
    [
        Value("Host.ShutdownTimeoutSeconds", () => WhenConfigured(Settings.ShutdownTimeoutKey, () => Seconds(settings.ShutdownTimeout))),
        Value("Limits.ExternalIntegrationsBatchSize", () => WhenConfigured(Settings.ExternalIntegrationsDispatchingBatchSizeKey, () => Number(settings.ExternalIntegrationsDispatchingBatchSize))),
        Value("Ingestion.Error.MaxConcurrency", () => WhenConfigured(Settings.MaximumConcurrencyLevelKey, () => Number(settings.MaximumConcurrencyLevel))),
        Value("Ingestion.Error.BatchSize", () => WhenConfigured(nameof(Settings.ErrorIngestionBatchSize), () => Number(settings.ErrorIngestionBatchSize))),
        Value("Ingestion.Error.MaxParallelWriters", () => WhenConfigured(nameof(Settings.ErrorIngestionMaxParallelWriters), () => Number(settings.ErrorIngestionMaxParallelWriters))),
        Value("Ingestion.Error.BatchTimeoutMs", () => WhenConfigured(nameof(Settings.ErrorIngestionBatchTimeout), () => Milliseconds(settings.ErrorIngestionBatchTimeout))),
        Value("Ingestion.Error.RestartAfterFailureSeconds", () => WhenConfigured(Settings.TimeToRestartErrorIngestionAfterFailureKey, () => Seconds(settings.TimeToRestartErrorIngestionAfterFailure))),
        Value("Heartbeats.GracePeriodSeconds", () => WhenConfigured(Settings.HeartbeatGracePeriodKey, () => Seconds(settings.HeartbeatGracePeriod))),
        Value("Recoverability.RetryHistoryDepth", () => WhenConfigured(Settings.RetryHistoryDepthKey, () => Number(settings.RetryHistoryDepth)))
    ];

    static string WhenConfigured(string key, Func<string> readValue) =>
        SettingsReader.TryRead<string>(Settings.SettingsRootNamespace, key, out _) ? readValue() : "Default";

    static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

    static string Seconds(TimeSpan value) =>
        Math.Round(value.TotalSeconds, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);

    static string Milliseconds(TimeSpan value) =>
        Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture);
}
