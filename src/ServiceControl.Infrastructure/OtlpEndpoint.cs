namespace ServiceControl.Infrastructure;

using System;

public static class OtlpEndpoint
{
    public static string MetricsEndpointFromEnvironment() =>
        Read("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT") ?? Read("OTEL_EXPORTER_OTLP_ENDPOINT");

    static string Read(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
