using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TestingTool;

/// <summary>
/// Configures OpenTelemetry traces, metrics, and logs for the testing tool. All telemetry is
/// exported via OTLP to the endpoint configured by <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (set in the
/// docker-compose environment). A Prometheus scraping endpoint is also exposed at <c>/metrics</c>.
/// </summary>
public static class TelemetrySetup
{
    public const string ServiceName = "testing-tool";
    public const string MeterName = "testing-tool";

    // Shared activity source names used across the tool.
    public static class Sources
    {
        public const string Load = "testing-tool.load";
        public const string Replay = "testing-tool.replay";
        public const string Search = "testing-tool.search";
        public const string Bypass = "testing-tool.bypass";
    }

    public static Meter CreateMeter() => new(MeterName, "1.0.0");

    public static OpenTelemetryBuilder AddTestingToolTelemetry(this IServiceCollection services, Meter meter)
    {
        return services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(Sources.Load)
                .AddSource(Sources.Replay)
                .AddSource(Sources.Search)
                .AddSource(Sources.Bypass)
                // Also pick up per-scenario activity sources dynamically.
                .AddSource("testing-tool.*")
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(MeterName)
                .AddPrometheusExporter()
                .AddOtlpExporter())
            // Phase 1: Route structured logs through the OpenTelemetry logs API to OTLP.
            .WithLogging(l => l.AddOtlpExporter());
    }
}