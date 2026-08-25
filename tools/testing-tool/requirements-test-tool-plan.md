# Plan: Testing Tool (requirements-test-tool.md)

A load + real-world-scenario testing tool deployed alongside a test instance of ServiceControl to validate error ingestion performance.

> Source requirements: [requirements-test-tool.md](./requirements-test-tool.md)

---

## 1. Architecture Overview

The tool is a **stateless, horizontally-scalable .NET service** hosted in a container that generates error load against a test ServiceControl instance. It exposes a minimal web UI for manual scenario triggers and emits OpenTelemetry (otel) traces/metrics/logs for everything.

```
                ┌────────────────────────────────────────────┐
                │              Testing Tool Pod(s)            │
                │                                            │
                │  ┌──────────┐  ┌────────────────────────┐  │
                │  │  Web UI  │  │  Scenario Hosted Service│  │
                │  │ (manual  │  │  - Background replay    │  │
                │  │  trigger)│  │  - Background search    │  │
                │  └────┬─────┘  │  - Load generators      │  │
                │       │        └───────────┬────────────┘  │
                │       │                    │               │
                │       ▼                    ▼               │
                │  ┌──────────────────────────────────────┐  │
                │  │   OTel SDK (traces/metrics/logs)      │  │
                │  └──────────────────┬───────────────────┘  │
                └─────────────────────┼──────────────────────┘
                                      │
                       ┌──────────────▼──────────────┐
                       │  OTel Collector / Jaeger    │
                       └─────────────────────────────┘
                                      │
                       ┌──────────────▼──────────────┐
                       │  ServiceControl (test inst) │
                       │  - Error ingestion queue    │
                       │  - FTS index (RavenDB)      │
                       └─────────────────────────────┘
```

### Key design decisions

| Concern | Decision | Rationale |
|---|---|---|
| Framework | ASP.NET Core 8 minimal API + `IHostedService` | Stateless, container-friendly, first-class otel + DI |
| Error transport | NServiceBus endpoint sending failed messages to ServiceControl error queue | Matches real handler path (requirement) |
| Direct injection | Optional raw `IMessagingDispatcher`/queue writer to bypass handler for high load | Requirement: bypass initial message creation |
| State | In-memory only, no DB | Stateless + horizontal scale |
| Scaling | Run N replicas; each owns disjoint scenario slices via env-configured shard id | Stateless requirement |
| UI | Single static HTML page + JSON endpoints | "simple web ui" |

---

## 2. Work Breakdown (with progress tracking)

### Phase 0 — Project bootstrap

- [x] Create solution `TestingTool.slnx` with projects:
  - `TestingTool` (web + hosted services)
  - `TestingTool.Scenarios` (scenario definitions)
  - `TestingTool.Contracts` (shared DTOs for UI API)
- [x] Add `Dockerfile` (multi-stage, `chiseled` base)
- [x] Add `docker-compose.yml` with ServiceControl test instance + Jaeger (OTLP) collector
- [x] Add `.github/workflows/testing-tool-ci.yml` (build + container image build; test step deferred — no tests exist in Phase 0)

### Phase 1 — OTel foundation

> Requirement: *"Everything should expose otel"*

- [x] Add NuGet refs: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`
- [x] Configure OTLP exporter via env `OTEL_EXPORTER_OTLP_ENDPOINT`
- [x] Define `ActivitySource` instances per scenario category (`testing-tool.load`, `testing-tool.replay`, `testing-tool.search` + per-scenario sources)
- [x] Add metrics: `errors_sent_total{scenario}`, `errors_replayed_total{group}`, `searches_executed_total{query}`, `search_latency_ms{query}` (histogram)
- [x] Prometheus scraping endpoint at `/metrics`
- [x] Add structured logs routed through OTel logs API (Phase 1 complete)

```csharp
// Program.cs — OTel wiring
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("testing-tool",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("testing-tool.load")
        .AddSource("testing-tool.replay")
        .AddSource("testing-tool.search")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("testing-tool")
        .AddPrometheusExporter()      // optional /metrics for ad-hoc
        .AddOtlpExporter())
    .WithLogging(l => l.AddOtlpExporter());

builder.Services.AddOpenTelemetryPrometheusScrapingEndpoint();
```

### Phase 2 — NServiceBus endpoint + error path

> Requirements: *"generate errors via a real message handler"*, *"simulate high error loads … bypass actually creating the initial messages"*

- [x] Configure NServiceBus endpoint `TestingTool.Load` with error queue pointed at ServiceControl (using NServiceBus 10 `AddNServiceBusEndpoint` DI integration + Learning transport)
- [x] Implement `FailingMessageHandler` that throws based on injected `IScenario` (via `IScenarioRegistry`)
- [x] Implement `DirectErrorQueueWriter` that constructs `MessageFailed` / transport message envelopes and writes directly to the ServiceControl error queue (bypass path) — see ref [ServiceControlFeeder](https://github.com/dvdstelt/ServiceControlFeeder)
- [x] Add a load-rate controller (token bucket) configurable per scenario (`PeriodicTimer`-based rate controller in `ScenarioRunner`)

```csharp
// FailingMessageHandler.cs
public class FailingMessageHandler(IScenario scenario, ILogger<FailingMessageHandler> log)
    : IHandleMessages<SampleCommand>
{
    public Task Handle(SampleCommand message, IMessageHandlerContext context)
    {
        using var activity = scenario.ActivitySource.StartActivity("handler");
        activity?.SetTag("scenario", scenario.Name);

        if (scenario.ShouldFail(context.MessageId))
            throw scenario.CreateException();  // routed to ServiceControl error queue

        return Task.CompletedTask;
    }
}
```

### Phase 3 — Scenarios

> Requirement: *"error generation should be based on some real scenarios, so that we still have nice groups of errors, example a third party outage"*

- [x] Define `IScenario` interface (with `Name`, `Description`, `Category`, `DefaultRate`, `ActivitySource`, `ShouldFail`, `CreateException`, `Cooldown`)
- [x] Implement scenarios:
  - [x] `ThirdPartyOutageScenario` — 100% fail for 20s burst, then 30s cooldown, repeat (groups by downstream host)
  - [x] `TimeoutSpikeScenario` — intermittent `TimeoutException` with correlated batch ids, oscillating rate
  - [x] `PoisonMessageScenario` — deterministic 15% fail on FNV-1a hash of message id (stays failed = retry storm)
  - [x] `DeserializationScenario` — 100% fail grouped by message type (simulates bad deployment)
  - [x] `RandomBackgroundNoiseScenario` — ~3% baseline error rate (always on), rotates through exception types
- [x] Tag each emitted exception with `ExceptionType`, `CorrelationGroup` (via `ScenarioException`) so ServiceControl groups them naturally

### Phase 4 — Background jobs

> Requirements: background replay job (errors should then pass), background search job (exercises FTS)

- [x] `ReplayService : BackgroundService`
  - Every configurable interval, fetches recent error groups from ServiceControl REST API and triggers replay
  - Emits otel activity + `errors_replayed_total` counter
  - Gated by `TestingTool:ReplayEnabled` config flag
  ```csharp
  protected override async Task ExecuteAsync(CancellationToken ct)
  {
      using var timer = new PeriodicTimer(_options.ReplayInterval);
      while (await timer.WaitForNextTickAsync(ct))
      {
          using var activity = _replaySource.StartActivity("replay-cycle");
          var groups = await _scClient.GetErrorGroupsAsync(ct);
          foreach (var g in groups.Where(ShouldReplay))
          {
              await _scClient.ReplayGroupAsync(g.Id, ct);
              _meter.CreateCounter<int>("errors_replayed_total")
                    .Add(g.Count, new("scenario", g.Classifier));
          }
      }
  }
  ```
- [x] `SearchService : BackgroundService`
  - Runs canned full-text-search queries against ServiceControl `/search` endpoint on a timer
  - Records latency histogram `search_latency_ms{query}`
  - Exercises FTS index under concurrent load
- [x] Both jobs are gated by config flags (`ReplayEnabled`, `SearchEnabled`) so replicas can opt-in/opt-out

### Phase 5 — Web UI

> Requirement: *"simple web ui for kicking off manual scenarios"*

- [x] Endpoints:
  - `GET  /`                  → static `index.html`
  - `GET  /api/scenarios`     → list available scenarios + status (with category, rate, error counts)
  - `POST /api/scenarios/{name}/start`  → start scenario (rate, durationSeconds)
  - `POST /api/scenarios/{name}/stop`
  - `POST /api/scenarios/stop-all`       → stop all running scenarios
  - `GET  /api/status`        → live counters snapshot (errors, replays, searches, active scenarios, rate, uptime, SC url)
  - `GET  /metrics`           → Prometheus scraping endpoint
- [x] `wwwroot/index.html` — vanilla JS SPA, no build step; dark/light theme, status dashboard, category-grouped scenario cards with Start/Stop + rate/duration controls, live 2s polling, toast notifications
- [x] Optional: wire release-test scenario names so they can be manually kicked off (requirement: *"any scenarios from the release tests should be considered to kick off manually"*)

```html
<!-- wwwroot/index.html (excerpt) -->
<table id="scenarios"><tbody></tbody></table>
<script>
  const load = async () => {
    const res = await fetch('/api/scenarios').then(r => r.json());
    document.querySelector('#scenarios tbody').innerHTML = res.map(s => `
      <tr>
        <td>${s.name}</td>
        <td>${s.running ? 'running' : 'idle'}</td>
        <td><button onclick="start('${s.name}')">Start</button>
            <button onclick="stop('${s.name}')">Stop</button></td>
      </tr>`).join('');
  };
  const start = n => fetch(`/api/scenarios/${n}/start`, {method:'POST'}).then(load);
  const stop  = n => fetch(`/api/scenarios/${n}/stop`, {method:'POST'}).then(load);
  load(); setInterval(load, 2000);
</script>
```

### Phase 6 — Containerization & scaling

> Requirements: *"hosted in a container"*, *"stateless"*, *"can be scaled horizontally"*

- [x] Multi-stage `Dockerfile` (chiseled composite base, .NET 10, multi-arch build)
- [x] No local file/db state; all config via env vars (`TestingTool__*` section binding + `SHARD_ID` + `OTEL_*`)
- [x] Shard id derived from pod ordinal/hostname → disjoint scenario slices across replicas (`ShardIdResolver`: env var → StatefulSet ordinal → MachineName)
- [x] `docker-compose` (single replica, local dev) + k8s `StatefulSet` (3 replicas, configurable) with HTTP liveness/readiness probes on `/health/live` and `/health/ready`
- [x] Health endpoints: `GET /health/live` (liveness) + `GET /health/ready` (readiness)
- [x] Document horizontal scale: *N replicas each emit 1/N of target rate* (README § Horizontal scaling + Configuration + Health checks)

### Phase 7 — Observability dashboard & verification

- [ ] ~~Ship a prebuilt Grafana dashboard JSON (errors/sec, ingestion lag, search p95, replay success)~~ — **unplanned** (otel traces/metrics already flow to the collector; a bespoke dashboard is out of scope)
- [x] Create an Aspire AppHost that orchestrates the testing tool together with the full Particular platform (ServiceControl + transport + persistence), so a single `aspire run` brings up the whole system locally. Include tag/channel selection for the platform images so a specific ServiceControl version (or `latest`) can be pinned via the Aspire app model — see the [Aspire docs on container image tag selection](https://learn.microsoft.com/dotnet/aspire/fundamentals/containers) for the `WithImageTag` / `WithImage(...)` resource customization pattern.
- [x] Add a smoke test that: starts tool → triggers `ThirdPartyOutageScenario` for 30s → verifies errors appear in ServiceControl → verifies replay passes
- [x] Write README with run instructions + env var reference

---

## 3. References

- **Source requirements:** [requirements-test-tool.md](./requirements-test-tool.md)
- **ServiceControlFeeder** — direct error-queue feeding reference: https://github.com/dvdestelt/ServiceControlFeeder
  - Pattern to reuse: raw transport-message construction written to the ServiceControl `error` queue to bypass the handler path (Phase 2 `DirectErrorQueueWriter`).
- **FakeMessageGen** — high-throughput fake message generator reference: https://github.com/ramonsmits/FakeMessageGen
  - Pattern to reuse: rate-controlled message generation loop and token-bucket shaping (Phase 2 load controller).
- **NServiceBus** — `SendFailedMessagesTo("error")` routes failed messages to ServiceControl. Docs: https://docs.particular.net/nservicebus/recoverability
- **ServiceControl REST API** — `/search`, error group listing, and replay endpoints used by the background jobs. Docs: https://docs.particular.net/servicecontrol/
- **OpenTelemetry .NET** — `AddOpenTelemetry()` host integration. Docs: https://opentelemetry.io/docs/instrumentation/net/
- **Out of scope:** Audit testing (per requirements).

---

## 4. Open questions

- [ ] Which ServiceControl version(s) are the test target? (affects REST API shape)
- [ ] Direct error-queue writer: transport = MSMQ / SQL / ASB / ASQ / RabbitMQ / SQS? Changes envelope format.
- [ ] Release-test scenarios: is there an existing manifest file to import, or define new ones here?
- [ ] Target error throughput ceiling (helps size replicas + token bucket defaults)?
- [ ] Where should the Grafana dashboard + compose live — this repo or a shared infra repo?

---

## 5. Milestone summary

| Milestone | Deliverable | Phase |
|---|---|---|
| M1 | OTel-instrumented endpoint emitting grouped errors (handler + bypass paths) | Phases 0–3 ✅ |
| M2 | Background replay + search jobs running on timers | Phase 4 ✅ |
| M3 | Web UI for manual scenario control | Phase 5 ✅ |
| M4 | Containerized, horizontally-scalable deploy + Aspire AppHost + smoke test | Phases 6–7 ✅ |