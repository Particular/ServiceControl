using Aspire.Hosting.ApplicationModel;

namespace TestingTool.AppHost;

/// <summary>
/// Adds a complete OpenTelemetry observability stack to the Aspire AppHost, giving purpose-built
/// trace analysis (Jaeger) and metric dashboards (Grafana + Prometheus) that are richer than the
/// Aspire dashboard's built-in OTel view.
///
/// Architecture:
///   testing-tool → OTLP → OTel Collector → traces → Jaeger
///                                    └→ metrics → Prometheus exporter ← Prometheus ← Grafana
///
/// All config files live under <c>obs/</c> next to the AppHost project and are bind-mounted into
/// the containers at startup.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds the observability stack (OTel Collector, Jaeger, Prometheus, Grafana) and returns
    /// references so callers can wire the testing tool's OTLP exporter at the collector.
    /// </summary>
    public static ObservabilityStack AddObservabilityStack(this IDistributedApplicationBuilder builder)
    {
        var jaeger = AddJaeger(builder);
        var collector = AddOtelCollector(builder, jaeger);
        var prometheus = AddPrometheus(builder, collector);
        var grafana = AddGrafana(builder, prometheus, jaeger);

        // Grafana is the user-facing entry point of the stack, so nest the backing
        // OTel/trace/metric resources under it in the Aspire dashboard's resource tree.
        collector.WithParentRelationship(grafana);
        jaeger.WithParentRelationship(grafana);
        prometheus.WithParentRelationship(grafana);

        return new ObservabilityStack(collector, grafana, jaeger, prometheus);
    }

    /// <summary>
    /// Overrides the image tag for every ServiceControl container in the AppHost. Pass a tag as
    /// the first <c>aspire run</c> argument (e.g. <c>aspire run -- pr-1234</c>) to test a specific
    /// prerelease. Omit it to use the default tag configured by the platform.
    /// </summary>
    public static void UseServiceControlImageTag(this IDistributedApplicationBuilder builder, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        Console.WriteLine($"Using ServiceControl image tag: {tag}");
        foreach (var c in builder.Resources.OfType<ContainerResource>())
        {
            if (c.TryGetLastAnnotation<ContainerImageAnnotation>(out var image) &&
                image.Image.StartsWith("particular/servicecontrol"))
            {
                builder.CreateResourceBuilder(c)
                    .WithImage($"ghcr.io/{image.Image}", tag);
            }
        }
    }

    // --- Jaeger: distributed-trace UI ---

    static IResourceBuilder<ContainerResource> AddJaeger(IDistributedApplicationBuilder builder) =>
        builder.AddContainer("jaeger", "jaegertracing/all-in-one:1.62.0")
            .WithHttpEndpoint(16686, 16686, "ui")
            .WithEndpoint(4317, 4317, scheme: "http", name: "otlp-grpc")
            .WithHttpEndpoint(4318, 4318, "otlp-http")
            .WithUrlForEndpoint("ui", url => url.DisplayText = "Jaeger UI — Traces");

    // --- OTel Collector: receives OTLP, fans out traces → Jaeger, metrics → Prometheus exporter ---

    static IResourceBuilder<ContainerResource> AddOtelCollector(
        IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> jaeger) =>
        builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib:0.110.0")
            // The contrib image reads its config from /etc/otelcol-contrib/config.yaml (the core
            // image uses /etc/otelcol/config.yaml). Mounting at the wrong path silently leaves the
            // image's built-in default config active — which has no Prometheus exporter, so the
            // :8889 scrape target has no listener and Prometheus gets "connection refused".
            .WithBindMount("obs/otel-collector-config.yaml", "/etc/otelcol-contrib/config.yaml")
            .WithHttpEndpoint(8889, 8889, "metrics")   // Prometheus scrape target
            // The third positional arg of WithEndpoint is `scheme`, not `name` — passing "otlp-grpc"
            // positionally would set UriScheme to "otlp-grpc" and yield an otlp-grpc:// URL that the
            // .NET OTLP gRPC exporter (GrpcChannel) rejects, silently dropping all telemetry. Name
            // the endpoint explicitly and force an `http` scheme so GetEndpoint produces an
            // http:// URL the exporter can connect to (gRPC runs over HTTP/2).
            .WithEndpoint(4317, 4317, scheme: "http", name: "otlp-grpc")     // OTLP gRPC (testing tool sends here)
            .WithHttpEndpoint(4318, 4318, "otlp-http") // OTLP HTTP (fallback)
            .WaitFor(jaeger)
            .WithUrlForEndpoint("metrics", url => url.DisplayText = "OTel Collector — Prometheus Metrics");

    // --- Prometheus: scrapes the collector's metrics exporter ---

    static IResourceBuilder<ContainerResource> AddPrometheus(
        IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> collector) =>
        builder.AddContainer("prometheus", "prom/prometheus:v3.2.1")
            .WithBindMount("obs/prometheus.yml", "/etc/prometheus/prometheus.yml")
            .WithHttpEndpoint(9090, 9090, "http")
            .WaitFor(collector)
            .WithUrlForEndpoint("http", url => url.DisplayText = "Prometheus — Metrics");

    // --- Grafana: dashboards with Prometheus + Jaeger data sources ---

    static IResourceBuilder<ContainerResource> AddGrafana(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ContainerResource> prometheus,
        IResourceBuilder<ContainerResource> jaeger)
    {
        var grafana = builder.AddContainer("grafana", "grafana/grafana-oss:11.4.0")
            .WithBindMount("obs/grafana/provisioning", "/etc/grafana/provisioning")
            .WithBindMount("obs/grafana/dashboards", "/var/lib/grafana/dashboards")
            .WithHttpEndpoint(3000, 3000, "http")
            .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
            .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
            .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
            .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Viewer")
            .WaitFor(prometheus)
            .WaitFor(jaeger);

        grafana.WithUrlForEndpoint("http", url => url.DisplayText = "Grafana — Dashboards");
        return grafana;
    }
}

/// <summary>
/// References to the observability stack resources, returned from <see cref="ObservabilityExtensions.AddObservabilityStack"/>.
/// </summary>
public sealed record ObservabilityStack(
    IResourceBuilder<ContainerResource> Collector,
    IResourceBuilder<ContainerResource> Grafana,
    IResourceBuilder<ContainerResource> Jaeger,
    IResourceBuilder<ContainerResource> Prometheus);