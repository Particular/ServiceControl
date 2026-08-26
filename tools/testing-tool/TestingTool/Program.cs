using System.Diagnostics.Metrics;
using TestingTool;
using TestingTool.Contracts;
using TestingTool.Scenarios;

// --- Configuration ---

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TestingToolOptions>(builder.Configuration.GetSection("TestingTool"));

var options = builder.Configuration.GetSection("TestingTool").Get<TestingToolOptions>() ?? new TestingToolOptions();
var shardId = ShardIdResolver.Resolve();

// --- OpenTelemetry (Phase 1) ---

var meter = TelemetrySetup.CreateMeter();
builder.Services.AddTestingToolTelemetry(meter);
builder.Services.AddSingleton(meter);

// --- NServiceBus endpoint (Phase 2) ---

builder.Services.AddTestingToolEndpoint(options);

// --- Scenarios (Phase 3) ---

builder.Services.AddSingleton<IScenario>(_ => new ThirdPartyOutageScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new TimeoutSpikeScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new PoisonMessageScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new DeserializationScenario(shardId));
builder.Services.AddSingleton<IScenario>(_ => new RandomBackgroundNoiseScenario(shardId));

builder.Services.AddSingleton<IScenarioRegistry, ScenarioRegistry>();
builder.Services.AddSingleton<ScenarioRunner>();
builder.Services.AddSingleton<DirectErrorQueueWriter>();
builder.Services.AddSingleton<TestingToolMetrics>();

// --- Background jobs (Phase 4) ---

builder.Services.AddHttpClient<ServiceControlClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TestingToolOptions>>().Value;
    client.BaseAddress = new Uri(opts.ServiceControlApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<ReplayService>();
builder.Services.AddHostedService<SearchService>();

// --- Application pipeline ---

var app = builder.Build();
app.UseStaticFiles();
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

var metrics = app.Services.GetRequiredService<TestingToolMetrics>();
var scClient = app.Services.GetRequiredService<ServiceControlClient>();
var startedAt = DateTimeOffset.UtcNow;

// Auto-start background noise after the endpoint is ready.
app.Lifetime.ApplicationStarted.Register(() =>
{
    if (options.AutoStartBackgroundNoise)
    {
        var runner = app.Services.GetRequiredService<ScenarioRunner>();
        runner.TryStart("background-noise", null, null, out _);
    }
});

// Graceful shutdown: stop all scenarios and bypass writer.
app.Lifetime.ApplicationStopping.Register(() =>
{
    var runner = app.Services.GetRequiredService<ScenarioRunner>();
    runner.StopAll();
    app.Services.GetRequiredService<DirectErrorQueueWriter>().Stop();
});

// --- Health endpoints (Phase 6) ---
// Lightweight probe targets for Kubernetes liveness/readiness and docker-compose health checks.
// The chiseled runtime image has no shell/curl, so HTTP probes are used instead of exec probes.

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", shardId }));

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", shardId }));

// --- API endpoints (Phase 5) ---

app.MapGet("/api/scenarios", (ScenarioRunner runner) => Results.Ok(runner.GetSnapshot()));

app.MapGet("/api/status", () => Results.Ok(new TestingToolStatus
{
    Ready = true,
    ErrorsSent = metrics.TotalErrorsSent,
    ErrorsReplayed = metrics.TotalErrorsReplayed,
    SearchesExecuted = metrics.TotalSearches,
    BypassErrorsWritten = metrics.TotalBypassErrorsWritten,
    ShardId = shardId,
    ActiveScenarios = metrics.ActiveScenarios,
    CurrentRate = Math.Round(metrics.CurrentRate, 1),
    ReplayEnabled = options.ReplayEnabled,
    SearchEnabled = options.SearchEnabled,
    ServiceControlUrl = scClient.BaseUrl,
    Uptime = (DateTimeOffset.UtcNow - startedAt).ToString(@"h\h\ m\m\ s\s")
}));

app.MapPost("/api/scenarios/{name}/start", (string name, StartScenarioRequest? request, ScenarioRunner runner) =>
{
    var duration = request?.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!runner.TryStart(name, request?.Rate, duration, out var error))
        return Results.BadRequest(new { error });

    var snapshot = runner.GetSnapshot()
        .First(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(snapshot);
});

app.MapPost("/api/scenarios/{name}/stop", (string name, ScenarioRunner runner) =>
{
    if (!runner.TryStop(name))
        return Results.BadRequest(new { error = $"Scenario '{name}' is not running" });

    return Results.Ok(new { stopped = name });
});

app.MapPost("/api/scenarios/stop-all", (ScenarioRunner runner) =>
{
    runner.StopAll();
    return Results.Ok(new { stopped = "all" });
});

// --- Bypass endpoints (Phase 2: direct error-queue writer) ---
// These endpoints control the bypass path that writes failed-message envelopes directly to the
// ServiceControl error queue, bypassing the handler for high-throughput error load.

app.MapGet("/api/bypass/status", (DirectErrorQueueWriter writer) => Results.Ok(writer.GetStatus()));

app.MapPost("/api/bypass/start", (StartBypassRequest? request, DirectErrorQueueWriter writer, IScenarioRegistry registry) =>
{
    var scenarioName = request?.Scenario;
    if (string.IsNullOrWhiteSpace(scenarioName))
    {
        // Default to the first scenario if none specified.
        scenarioName = registry.All[0].Name;
    }

    var rate = request?.Rate ?? 100;
    var duration = request?.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!writer.TryStart(scenarioName, rate, duration, out var error))
        return Results.BadRequest(new { error });

    return Results.Ok(writer.GetStatus());
});

app.MapPost("/api/bypass/stop", (DirectErrorQueueWriter writer) =>
{
    writer.Stop();
    return Results.Ok(writer.GetStatus());
});

// --- Release-test scenario endpoints (Phase 5: release-test presets) ---
// Lists release-test presets that map to testing-tool scenarios, and allows kicking them off
// by release-test name. This satisfies the optional requirement to consider release-test
// scenarios for manual kickoff.

app.MapGet("/api/release-tests", () => Results.Ok(ReleaseTestScenarios.Presets.Select(p => new
{
    name = p.Name,
    scenario = p.ScenarioName,
    description = p.Description,
    rate = p.Rate,
    durationSeconds = p.DurationSeconds
})));

app.MapPost("/api/release-tests/{name}/start", (string name, ScenarioRunner runner) =>
{
    var preset = ReleaseTestScenarios.Find(name);
    if (preset is null)
        return Results.BadRequest(new { error = $"Unknown release-test scenario '{name}'" });

    var duration = preset.DurationSeconds is { } secs and > 0
        ? TimeSpan.FromSeconds(secs)
        : (TimeSpan?)null;

    if (!runner.TryStart(preset.ScenarioName, preset.Rate, duration, out var error))
        return Results.BadRequest(new { error });

    return Results.Ok(new { started = preset.Name, scenario = preset.ScenarioName, rate = preset.Rate });
});

app.MapFallbackToFile("index.html");

app.Run();