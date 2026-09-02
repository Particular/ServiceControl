# ServiceControl Testing Tool

A stateless, horizontally-scalable .NET 10 service that generates error load and real-world failure
scenarios against a test ServiceControl instance, with OpenTelemetry observability and a simple web
UI for manual scenario control.

See [requirements-test-tool-plan.md](./requirements-test-tool-plan.md) for the full plan.

## Status

All phases complete (0–7): project bootstrap, OTel foundation (traces + metrics + logs),
NServiceBus error path (handler + direct error-queue bypass writer), scenarios, background jobs,
web UI, containerization & scaling, Aspire AppHost, observability stack (OTel Collector →
Jaeger + Prometheus + Grafana with prebuilt dashboard), and smoke tests.

## What it does

The tool runs an NServiceBus endpoint (`TestingTool.Load`) that sends messages through a handler
which fails based on the active scenario. Failed messages are routed to the `error` queue for
ServiceControl to ingest. Five scenarios are built in, each producing naturally-grouped errors:

| Scenario | Category | Failure shape |
|---|---|---|
| `third-party-outage` | Outage | 100% fail for 20s bursts, 30s cooldown — grouped by downstream host |
| `timeout-spike` | Timeout | Oscillating 10–70% fail rate — grouped by 5-min batch bucket |
| `poison-message` | Poison | 15% deterministic always-fail messages — retry storm |
| `deserialization-failure` | Deserialization | 100% fail — grouped by message type (bad deployment) |
| `background-noise` | Noise | ~3% always-on baseline — rotates through exception types |

Recoverability/search jobs are controllable from the web UI (no longer hidden config-gated
 timers). They run a cycle on a configurable interval until stopped:
- **Retry** — fetches error groups from ServiceControl and retries each group
- **Archive** — fetches error groups from ServiceControl and archives each group
- **Search** — runs canned FTS queries to exercise the ServiceControl search index
- **Retention sweep** — triggers a manual retention sweep on ServiceControl each cycle,
  exercising the retention pipeline (full scan-and-delete of aged failures and event-log rows)
  against the load the other jobs produce

Jobs do not auto-start; start them from the UI (or `/api/jobs`) when needed. Control via:
- `GET /api/jobs` — list jobs with live status
- `POST /api/jobs/{name}/start` — `{ "intervalSeconds": 120 }` (omit for the job default)
- `POST /api/jobs/{name}/stop`
- `POST /api/jobs/stop-all`

All telemetry is exported via OTLP (traces + metrics + logs) and a Prometheus `/metrics` endpoint.

### Direct error-queue bypass writer

In addition to the handler path, the tool can write failed-message envelopes directly to the
ServiceControl error queue, bypassing the handler entirely for high-throughput error load.
Each message carries standard NServiceBus failure headers (`NServiceBus.ExceptionInfo.*`,
`NServiceBus.FailedQ`) so ServiceControl ingests it as a genuine failed message. Control via:
- `POST /api/bypass/start` — `{ "scenario": "third-party-outage", "rate": 100, "durationSeconds": 60 }`
- `POST /api/bypass/stop`
- `GET /api/bypass/status`

### Release-test scenario presets

The tool ships with presets mapped from `docs/testing-scenarios.md` so release-test scenarios can
be kicked off manually by name:
- `GET /api/release-tests` — list all presets
- `POST /api/release-tests/{name}/start` — start a preset (e.g. `retry-message-group`, `ingestion-load`)

## Layout

```
tools/testing-tool/
  TestingTool.slnx
  Directory.Build.props          # repo-style conventions (warnings-as-errors, nullable, analyzers)
  Dockerfile
  global.json
  TestingTool/                   # ASP.NET Core host: Program.cs, web UI, services
    TestingTool.csproj
    Program.cs                   # OTel wiring, NServiceBus endpoint, DI, API endpoints
    appsettings.json             # base config (TestingTool section, overridable by env vars)
    appsettings.Development.json # Development overrides
    wwwroot/index.html           # single-page web UI (vanilla JS, no build step)
    ScenarioRunner.cs            # start/stop, rate control, per-scenario error counting
    DirectErrorQueueWriter.cs    # bypass path: writes failed-message envelopes directly to error queue
    Jobs/                        # UI-controllable recoverability/search jobs (retry, archive, search)
      JobBase.cs                 # periodic job base class (start/stop, cycle counters)
      JobRunner.cs               # manages job lifecycle, exposes /api/jobs
      RetryJob.cs                # retries all error groups each cycle
      ArchiveJob.cs              # archives all error groups each cycle
      SearchJob.cs               # canned FTS queries each cycle
      RetentionSweepJob.cs       # triggers a manual retention sweep each cycle
    FailingMessageHandler.cs     # NServiceBus handler that throws per scenario logic
    ReleaseTestScenarios.cs      # release-test preset mappings (Phase 5)
    ServiceControlClient.cs      # REST API client (error groups, retry, archive, search)
    TelemetrySetup.cs            # OTel traces + metrics + logs + OTLP/Prometheus exporters
    NServiceBusSetup.cs          # endpoint config (Learning transport, error queue routing)
    TestingToolOptions.cs        # config (SC URL, retry/archive/search intervals, error queue name)
    TestingToolMetrics.cs        # shared live counters for /api/status
    ShardIdResolver.cs           # shard id from env var, StatefulSet ordinal, or hostname
    IScenarioRegistry.cs         # scenario registry abstraction (DI)
    ScenarioRegistry.cs          # default scenario registry implementation
  TestingTool.Scenarios/         # IScenario contract + 5 scenario implementations
  TestingTool.Contracts/         # shared DTOs (ScenarioInfo, TestingToolStatus, BypassStatus, etc.)
  TestingTool.SmokeTests/        # nunit smoke tests (requires running SC + tool)
  TestingTool.AppHost/           # Aspire AppHost project (platform + tool + observability stack)
    AppHost.cs                   # top-level orchestration (platform, observability, testing tool)
    HostBuilderExtensions.cs     # persistence-type extensions (RavenDB / SQL Server / PostgreSQL)
    ObservabilityExtensions.cs   # AddObservabilityStack() — OTel Collector + Jaeger + Prometheus + Grafana
    PersistenceType.cs           # persistence enum
    obs/                         # observability config (collector, Prometheus, Grafana provisioning + dashboard)
      otel-collector-config.yaml # collector pipeline: traces → Jaeger, metrics → Prometheus exporter
      prometheus.yml             # scrape config (targets the collector's metrics exporter)
      grafana/provisioning/      # auto-provisioned data sources (Prometheus + Jaeger) and dashboard provider
      grafana/dashboards/        # prebuilt "Testing Tool" Grafana dashboard JSON
```

## Run locally

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290 (or the port shown in the console).

## Run with Aspire

The Aspire AppHost orchestrates the testing tool together with the full Particular platform
(ServiceControl + Learning transport + RavenDB + ServicePulse) and a complete observability
stack (OTel Collector, Jaeger, Prometheus, Grafana), so a single command brings up the whole
system locally:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

To test a specific ServiceControl image tag (e.g. a PR-based prerelease tag):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234
```

To select a persistence backend for the ServiceControl error instance (`RavenDb`,
`SqlServer`, or `PostgreSql`; defaults to `PostgreSql`):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --persistence:RavenDb
```

`--persistence RavenDb` (space separator) is accepted too. Both flags may be combined:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234 --persistence:SqlServer
```

The Aspire dashboard provides allocated ports for each service. The testing tool automatically
connects to ServiceControl via the platform's transport and REST API URL, and sends its OTLP
telemetry to the OTel Collector, which fans out traces to Jaeger and metrics to Prometheus.
Grafana (auto-provisioned with Prometheus + Jaeger data sources) provides a prebuilt dashboard
at the allocated port — log in with `admin`/`admin` or browse anonymously as Viewer.

### Observability stack

| Service | Image | Purpose |
|---|---|---|
| OTel Collector | `otel/opentelemetry-collector-contrib` | Receives OTLP, fans out traces → Jaeger, metrics → Prometheus exporter |
| Jaeger | `jaegertracing/all-in-one` | Distributed-trace UI — purpose-built trace analysis richer than the Aspire dashboard |
| Prometheus | `prom/prometheus` | Scrapes the collector's metrics exporter |
| Grafana | `grafana/grafana-oss` | Dashboards with auto-provisioned Prometheus + Jaeger data sources |

The stack is wired via `AddObservabilityStack()` in `ObservabilityExtensions.cs` so `AppHost.cs`
stays clean. Config files live under `obs/` next to the AppHost project. The prebuilt Grafana
dashboard ("Testing Tool — Error Load & Observability") shows errors/sec by scenario (handler
and bypass paths emitted separately and combined into the raised total), search latency p95,
replay/archive rates, and — using ServiceControl's own OTel ingestion metrics
(`sc.error.ingestion.*`) — side-by-side comparison of errors raised vs errors ingested (rate
and cumulative), ingestion duration p95, and ingestion outcome by result.

## Run smoke tests

The smoke tests require a running ServiceControl + testing tool (e.g. via the Aspire AppHost above,
or `dotnet run` against an existing ServiceControl):

```bash
# Start the stack first (see Run with Aspire)
dotnet test tools/testing-tool/TestingTool.SmokeTests
```

The test URLs default to `http://localhost:8080` (tool) and `http://localhost:33333` (ServiceControl).
Override them to match your run — Aspire assigns dynamic ports, shown in the Aspire dashboard:
```bash
TESTING_TOOL_URL=http://localhost:<tool-port> SERVICECONTROL_URL=http://localhost:<sc-port> \
  dotnet test tools/testing-tool/TestingTool.SmokeTests
```

## Horizontal scaling

The tool is **stateless** — all state is in-memory per replica. The repo no longer ships
docker-compose or Kubernetes manifests; run a single instance via `dotnet run` or the Aspire
AppHost. For multi-replica deployments, bring your own orchestration and give each replica a
distinct shard id so deterministic failure decisions don't overlap:

| Shard id source | When |
|---|---|
| `SHARD_ID` env var | Explicit override — recommended for any custom deployment |
| Hostname trailing ordinal (e.g. `testing-tool-2` → `2`) | StatefulSet-style ordered hostnames |
| `MachineName` | Fallback — unique per host/pod |

To achieve a target aggregate rate of R msg/s across N replicas, set each replica's scenario rate
to R/N. The web UI and `/api/status` endpoint report per-replica counters; aggregate across
replicas via Prometheus queries or the OTLP backend.

## Configuration

All configuration is via environment variables (no files, no database). Settings are in
`appsettings.json` under the `TestingTool` section, overridable by environment variables using
`__` as the section separator (e.g. `TestingTool__ServiceControlApiUrl`):

| Setting | Default | Description |
|---|---|---|
| `TestingTool__ServiceControlApiUrl` | `http://localhost:33333` | ServiceControl REST API base URL |
| `TestingTool__ReplayInterval` | `00:02:00` | Default interval for the retry job |
| `TestingTool__ReplayMinGroupSize` | `1` | Min messages in a group before retrying |
| `TestingTool__SearchInterval` | `00:01:00` | Default interval for the search job |
| `TestingTool__ArchiveInterval` | `00:02:00` | Default interval for the archive job |
| `TestingTool__ArchiveMinGroupSize` | `1` | Min messages in a group before archiving |
| `TestingTool__RetentionSweepInterval` | `00:05:00` | Default interval for the retention-sweep job |
| `TestingTool__ErrorQueueName` | `error` | NServiceBus error queue (ServiceControl monitors this) |
| `TestingTool__AutoStartBackgroundNoise` | `false` | Auto-start the background-noise scenario on startup |
| `SHARD_ID` (env) | *(auto: hostname ordinal or machine name)* | Shard id for disjoint scenario slices when scaled |
| `OTEL_EXPORTER_OTLP_ENDPOINT` (env) | `http://localhost:4317` | OTLP collector endpoint |
| `OTEL_SERVICE_NAME` (env) | `testing-tool` | OTel service name |

## Health checks

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — process is alive |
| `GET /health/ready` | Readiness — app is ready to serve requests |
| `GET /api/status` | Full status snapshot (counters, shard, uptime) |
| `GET /metrics` | Prometheus scraping endpoint |