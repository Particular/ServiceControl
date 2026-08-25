# ServiceControl Testing Tool

A stateless, horizontally-scalable .NET 10 service that generates error load and real-world failure
scenarios against a test ServiceControl instance, with OpenTelemetry observability and a simple web
UI for manual scenario control.

See [requirements-test-tool-plan.md](./requirements-test-tool-plan.md) for the full plan.

## Status

Phases 0–6 complete (project bootstrap, OTel foundation, NServiceBus error path, scenarios,
background jobs, web UI, containerization & scaling). Remaining: direct error-queue bypass writer
(Phase 2 optional item), Grafana dashboard + smoke test (Phase 7).

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

Two background jobs (gated by config) run on timers:
- **Replay** — fetches error groups from ServiceControl and triggers retry
- **Search** — runs canned FTS queries to exercise the RavenDB search index

All telemetry is exported via OTLP (traces + metrics) and a Prometheus `/metrics` endpoint.

## Layout

```
tools/testing-tool/
  TestingTool.slnx
  Directory.Build.props          # repo-style conventions (warnings-as-errors, nullable, analyzers)
  Directory.Packages.props       # central package management (OTel + NServiceBus pinned)
  Dockerfile
  docker-compose.yml
  k8s/testing-tool.yaml          # Kubernetes StatefulSet + Service + ConfigMap
  src/
    TestingTool/                 # ASP.NET Core host: Program.cs, web UI, services
      wwwroot/index.html         # single-page web UI (vanilla JS, no build step)
      Program.cs                 # OTel wiring, NServiceBus endpoint, DI, API endpoints
      ScenarioRunner.cs          # start/stop, rate control, per-scenario error counting
      FailingMessageHandler.cs   # NServiceBus handler that throws per scenario logic
      ServiceControlClient.cs    # REST API client (error groups, replay, search)
      ReplayService.cs           # background replay job
      SearchService.cs           # background search job
      TelemetrySetup.cs          # OTel traces + metrics + OTLP/Prometheus exporters
      NServiceBusSetup.cs        # endpoint config (Learning transport, error queue routing)
      TestingToolOptions.cs      # config (SC URL, replay/search intervals, error queue name)
      TestingToolMetrics.cs      # shared live counters for /api/status
      ShardIdResolver.cs         # shard id from env var, StatefulSet ordinal, or hostname
    TestingTool.Scenarios/       # IScenario contract + 5 scenario implementations
    TestingTool.Contracts/       # shared DTOs (ScenarioInfo, TestingToolStatus, StartScenarioRequest)
```

## Run locally

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/src/TestingTool --configuration Release
```

Open http://localhost:5290 (or the port shown in the console).

## Run the stack

```bash
docker compose -f tools/testing-tool/docker-compose.yml up --build
```

This starts ServiceControl + the testing tool + Jaeger (OTLP). Open:
- Testing tool UI: http://localhost:8080
- ServiceControl: http://localhost:33333
- Jaeger UI: http://localhost:16686
- Prometheus metrics: http://localhost:8080/metrics

## Deploy on Kubernetes

```bash
kubectl apply -f tools/testing-tool/k8s/
```

The StatefulSet runs 3 replicas by default. Each pod derives its shard id from its StatefulSet
ordinal (`testing-tool-0` → shard `0`, `testing-tool-1` → shard `1`, …) so replicas own disjoint
scenario slices automatically. Scale by changing `spec.replicas` in the manifest.

## Horizontal scaling

The tool is **stateless** — all state is in-memory per replica. When scaled to N replicas, each
replica emits its own share of load. Shard ids ensure deterministic failure decisions don't
overlap across pods:

| Replicas | Shard id source | Scenario slice |
|---|---|---|
| 1 (docker-compose) | `SHARD_ID=0` env var | All scenarios |
| N (k8s StatefulSet) | Pod ordinal from hostname | 1/N of each scenario's messages |

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
| `TestingTool__ReplayEnabled` | `false` | Enable the background replay job |
| `TestingTool__ReplayInterval` | `00:02:00` | Interval between replay cycles |
| `TestingTool__ReplayMinGroupSize` | `1` | Min messages in a group before replaying |
| `TestingTool__SearchEnabled` | `false` | Enable the background search job |
| `TestingTool__SearchInterval` | `00:01:00` | Interval between search cycles |
| `TestingTool__ErrorQueueName` | `error` | NServiceBus error queue (ServiceControl monitors this) |
| `TestingTool__AutoStartBackgroundNoise` | `false` | Auto-start the background-noise scenario on startup |
| `SHARD_ID` (env) | *(auto: pod ordinal or hostname)* | Shard id for disjoint scenario slices when scaled |
| `OTEL_EXPORTER_OTLP_ENDPOINT` (env) | `http://localhost:4317` | OTLP collector endpoint |
| `OTEL_SERVICE_NAME` (env) | `testing-tool` | OTel service name |

## Health checks

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Liveness — process is alive |
| `GET /health/ready` | Readiness — app is ready to serve requests |
| `GET /api/status` | Full status snapshot (counters, shard, uptime) |
| `GET /metrics` | Prometheus scraping endpoint |