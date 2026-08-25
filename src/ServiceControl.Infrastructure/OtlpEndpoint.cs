namespace ServiceControl.Infrastructure;

using System;
using Microsoft.Extensions.Configuration;

public static class OtlpEndpoint
{
    // This is the default environment variable name used by the OpenTelemetry .NET SDK to configure the OTLP exporter endpoint
    // as specified in https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
    const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static Uri Read(IConfiguration configuration)
    {
        var configuredEndpoint = configuration[EndpointKey];

        if (string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new UriFormatException($"Invalid {EndpointKey}: {configuredEndpoint}");
        }

        return endpoint;
    }
}
