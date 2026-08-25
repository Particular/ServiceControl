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

// Graceful shutdown: stop all scenarios.
app.Lifetime.ApplicationStopping.Register(() =>
{
    var runner = app.Services.GetRequiredService<ScenarioRunner>();
    runner.StopAll();
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

app.MapFallbackToFile("index.html");

app.Run();