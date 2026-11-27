# ServiceControl Testing Tool

A horizontally scalable .NET 10 service that generates failure scenarios, verifies smoke checks, and measures error-ingestion performance against a test ServiceControl instance. It includes OpenTelemetry observability and a web UI.

The tool drives both ingestion paths: failed messages into the primary ServiceControl instance, and audited messages and saga snapshots into a ServiceControl.Audit instance.

## What it does

The tool runs an NServiceBus endpoint (`TestingTool.Load`) that sends messages
through a handler which fails based on the active scenario. Failed messages are routed to the
`error` queue for ServiceControl to ingest. Each scenario throws a real exception type
from its own method, so ServiceControl's default grouping (exception type + first stack frame)
puts each failure shape in its own group, and immediate retries are disabled so groups stay clean. Six scenarios are built in:

| Scenario | Category | Failure shape |
|---|---|---|
| `third-party-outage` | Outage | 100% fail for 20s bursts, 30s cooldown — one `HttpRequestException` group |
| `timeout-spike` | Timeout | Oscillating 10–70% fail rate — one `TimeoutException` group |
| `poison-message` | Poison | 15% deterministic always-fail messages — retry storm |
| `deserialization-failure` | Deserialization | 100% fail with `SerializationException` (bad deployment) |
| `background-noise` | Noise | ~3% always-on baseline — rotates through four exception types (four small groups) |
| `large-messages` | Body | Starts at 3 msg/s (rate is configurable), with about 150 KiB of JSON text per body and about 190 KiB after message serialization. Exceeds the 100 KB external body storage threshold and leaves room for headers on Azure Service Bus Standard. 20% deterministic failures with `InvalidDataException`; the other 80% succeed and are audited |

Scenarios are controlled from the web UI, or via:
- `GET /api/scenarios` — list scenarios with live status (category, rate, error counts)
- `POST /api/scenarios/{name}/start` — `{ "rate": 100, "durationSeconds": 60 }`; `rate` defaults
  to the scenario's default, `durationSeconds` omitted (or `0`) means run until explicitly stopped
- `POST /api/scenarios/{name}/stop`
- `POST /api/scenarios/stop-all`

Recoverability/search jobs are controllable from the web UI. They run a cycle on a
configurable interval until stopped:
- **Retry** — fetches error groups from ServiceControl and retries each group; replayed messages
  return to the tool's endpoint and succeed (simulating a fix being applied), exercising
  ServiceControl's retry pipeline
- **Archive** — fetches error groups from ServiceControl and archives each group
- **Search** — runs canned FTS queries to exercise the ServiceControl search index
- **Retention sweep** — triggers a manual retention purge on ServiceControl each cycle
  (`POST /api/maintenance/retention/purge`), exercising the retention pipeline (full
  scan-and-delete of aged failures and event-log rows) against the load the other jobs
  produce. Each purge's cutoffs default to ServiceControl's configured retention periods;
  supply a cutoff timespan when starting the job (a field on the job card in the UI, or
  `cutoffTimespan` in the start request) to purge everything older than that instead
- **Custom check failures** — randomly reports internal-looking ServiceControl custom check
  failures to ServiceControl each cycle. Each cycle it sends a `ReportCustomCheckResult`
  per check in a pool of plausibly-named internal checks (category `ServiceControl Health`),
  randomly marking some as failed and the rest as passed, with `EndpointName` set to the
  ServiceControl instance name so they appear in ServicePulse as genuine internal custom
  checks. This exercises ServiceControl's custom-check ingestion and the ServicePulse Custom
  Checks dashboard under failure load, complementing the error-load scenarios.

Jobs do not auto-start; start them from the UI (or `/api/jobs`) when needed. Control via:
- `GET /api/jobs` — list jobs with live status
- `POST /api/jobs/{name}/start` — `{ "intervalSeconds": 120 }` (omit for the job default).
  The retention-sweep job also accepts `{ "cutoffTimespan": "14.00:00:00" }` (.NET timespan
  format) to send explicit purge cutoffs — rows older than `now − timespan` are deleted
  on every sweep; omit it to let ServiceControl derive cutoffs from its configured
  retention periods
- `POST /api/jobs/{name}/stop`
- `POST /api/jobs/stop-all`

All telemetry is exported via OTLP (traces + metrics + logs) and a Prometheus `/metrics` endpoint.

### Direct error-queue bypass writer

In addition to the handler path, the tool can write failed-message envelopes directly to the
ServiceControl error queue, bypassing the handler entirely for high-throughput error load.
Each message carries standard NServiceBus failure headers (`NServiceBus.ExceptionInfo.*`,
including a real stack trace, and `NServiceBus.FailedQ`) so ServiceControl ingests it as a genuine
failed message and groups it like the equivalent handler failure. Control via:
- `POST /api/bypass/start` — `{ "scenario": "third-party-outage", "rate": 100, "durationSeconds": 60 }`
- `POST /api/bypass/stop`
- `GET /api/bypass/status`

### Direct audit-queue writer

The audit-queue counterpart of the bypass writer. It writes audit envelopes straight to the ServiceControl.Audit instance's audit queue, so audit ingestion can be loaded without any endpoint ever processing a message. Control via:

- `POST /api/audit-bypass/start` — `{ "rate": 100, "parallelism": 4, "sagaSnapshotRatio": 0.2, "durationSeconds": 60 }`
- `POST /api/audit-bypass/stop`
- `GET /api/audit-bypass/status`

Most claims emit one processed-message envelope carrying the headers a real endpoint would have stamped: `NServiceBus.ProcessingEndpoint`, the host ids, and `TimeSent`, `ProcessingStarted` and `ProcessingEnded` spread so delivery, processing and critical times are not zero. They rotate across a pool of simulated endpoints, message types and conversations.

A share of claims instead emit a whole **saga episode**: the three messages that drove one saga through New, Updated and Completed, each followed by the snapshot it produced. The episode is written by a single worker so the phases reach the queue in order, and the pieces are linked the way a real deployment links them:

- each driving message carries `NServiceBus.InvokedSagas` (`SagaType:SagaId`) and `ServiceControl.SagaStateChange` (`SagaId:New|Updated|Completed`), which is the only thing that lets the audit instance attach saga information to an audited message
- each snapshot's `Initiator.InitiatingMessageId` is the id of the message directly before it, so drill-through from the saga view resolves to a real audited message
- the snapshot body is written in exact CLR casing, because ServiceControl deserializes it with a source-generated, case-sensitive context, and its `EnclosedMessageTypes` header is the bare unqualified type name, which is what the audit instance compares against to route a message to its saga branch

`sagaSnapshotRatio` is saga snapshots per processed message; the default `0.2` emits two saga snapshots for every ten processed messages. `0` disables saga episodes.

Audit ingestion needs a ServiceControl.Audit instance on SQL Server or PostgreSQL, so pair `--audit-instances` with `--storage:PostgreSql` or `--storage:SqlServer`. Under `--storage:RavenDb` the audit instance runs on RavenDB and the EF path is not exercised.

The testing tool's own endpoint also audits its successfully handled load messages to the same queue, which is what makes its installers create the queue before the writer first sends into it.

### Release-test scenario presets

The tool ships with presets mapped from [docs/testing-scenarios.md](/docs/testing-scenarios.md) so
release-test scenarios can be kicked off manually by name:
- `GET /api/release-tests` — list all presets
- `POST /api/release-tests/{name}/start` — start a preset (e.g. `retry-message-group`, `ingestion-load`)

### Smoke tests

The **Smoke tests** tab runs an API-verified version of the functionality checklist in [docs/testing-scenarios.md](/docs/testing-scenarios.md). **Run all** executes the automated checks one after another, **Stop** cancels the run, and each row shows pending, running, passed, failed, canceled or manual with the observed detail. The run state lives in the tool, so reloading the page shows the current or last run.

Every check sends its own messages with a run identifier (`smoke-<8 hex digits>`) and reads the effect back through the ServiceControl APIs. A check matches only what it created: a custom check id, an endpoint name, a message id, a conversation id, a saga id or a failure group that contains the run identifier. Background load and leftovers from earlier runs cannot satisfy a check. An HTTP 202 alone never passes a check, and a transport error or an unexpected status fails the check with the reason. When ServiceControl reports incomplete cross-instance results (`X-Particular-Incomplete-Results`), the check keeps polling and names the missing instances in the timeout detail; data that matches the run identifier still passes. Checks run in order, and a failed check does not stop later checks. Each check attempts cleanup after completion, cancellation, or shutdown.

| Check | What it does | What it verifies |
|---|---|---|
| `custom-check-fail-then-pass` | Reports a failed, then a passed custom check with id `Smoke.<run>` to the primary input queue | `GET /api/customchecks?status=fail` lists it with the failure reason, then `?status=pass` lists it and `?status=fail` does not |
| `heartbeat-beating-then-dead` | Starts a real in-process endpoint `TestingTool.Smoke.<run>` with the NServiceBus.Heartbeat plugin, then stops its host | `GET /api/endpoints` shows `heartbeat_information.reported_status` `beating`, then `dead` (about 45 seconds after the host stops with the default 40 second grace period; the check waits up to 100 seconds) |
| `audit-ingestion-fulltext` | Handles a message with a run-specific word in the body | `GET /api/messages/search?q=<word>` finds it with status `successful`, and `GET /api/messages/{id}/body` contains the word |
| `fanout-conversation` | Handles a message that sends two children per level for two levels, with a run-specific conversation id | `GET /api/conversations/{id}` returns the seven messages, and every `NServiceBus.RelatedTo` header points at a parent in the conversation |
| `saga-audit` | Runs a real saga (start, step, finish) with saga auditing to the audit queue | `GET /api/messages/search` finds the start message with its saga id, and `GET /api/sagas/{id}` lists `new`, `updated`, `completed` |
| `monitoring-data` | Starts a real endpoint `TestingTool.Smoke.<run>` with the NServiceBus.Metrics.ServiceControl plugin (2 second interval, as the load endpoint) and handles three messages that each take 100 ms | `GET /monitored-endpoints` on the monitoring instance lists the endpoint with a `processingTime` average above zero. Cleanup deletes the instance ids that monitoring reports for it |
| `integration-event` | Subscribes the tool endpoint to `ServiceControl.Contracts.MessageFailed` and fails one message | The published event arrives with the run header and the failed message id that `GET /api/recoverability/groups/{id}/errors` returns |
| `retry-single` | Fails two messages in a group unique to the run, then retries one with `POST /api/errors/{id}/retry` | The tool handled and `GET /api/errors/{id}` reports `resolved` for that message, and its sibling is unresolved and unhandled |
| `retry-group` | Fails three messages in a unique group, then retries the group with `POST /api/recoverability/groups/{id}/errors/retry` | Every message was handled and is `resolved` |
| `edit-and-retry` | Fails a message, then edits it with `POST /api/edit/{id}` (snake_case body, every original header, `Fail` cleared) | The edited message arrives with `ServiceControl.EditOf` set to the failed message id and the edited text |
| `retry-through-redirect` | Starts real endpoints `TestingTool.Smoke.<run>.Source` and `.Target`, fails a message in Source, creates a redirect from the Source input address to the Target input address with `POST /api/redirects`, retries it | The failed message has the Source address as its queue, the redirect is listed, the message is handled by Target (a retry that Source handles fails the check) and is `resolved`, and the redirect is gone after `DELETE /api/redirects/{id}`. It runs last because a redirect changes retry routing while it exists |

Failures come from the real message handler, not from the load scenarios. A smoke message fails when its `Fail` flag is set and its marker was not released, so a retry succeeds when the check releases the marker and an edit succeeds when it clears the flag. The handler throws a `SmokeFailureException` and the tool rewrites the fault headers so ServiceControl groups it under the exception type `TestingTool.Smoke.Failure_<run>_<check>`, which makes the group unique to one check. At the end of a check, unresolved failed messages of that check are archived. The redirect check removes only the redirect it created.

Prerequisites on the instance under test:

- The primary needs a remote audit instance for the audit, conversation and saga checks. With the Aspire AppHost, pass `--audit-instances 1`. The checks fail with that hint when `GET /api/configuration/remotes` is empty or a remote is not online.
- Integration events need `SERVICECONTROL_ENABLEEXTERNALINTEGRATIONSPUBLISHING=true` on the instances that ingest errors. The AppHost sets it on the primary and on the error-ingestion scale units.
- Message editing needs `SERVICECONTROL_ALLOWMESSAGEEDITING=true`. The AppHost sets it.
- The monitoring check needs `TestingTool__MonitoringApiUrl` to point at the monitoring instance. The AppHost sets it.
- Stop the retry, archive and retention-sweep jobs first: they act on every failed message group, so a start is refused while one runs. Scenarios and bypass writers may keep running.

Run it against a test instance only. Cleanup stops the run-scoped endpoint hosts, removes custom checks, endpoint and monitoring records, and redirects, and archives unresolved failures. Audited messages and resolved or archived failures remain subject to ServiceControl retention. If cleanup fails, the row reports the error and warns that data may remain.

Limits of the automation:

- The heartbeat, monitoring and redirect checks start real NServiceBus endpoints, each in its own host in the tool process, next to `TestingTool.Load`. They use the same transport, topology and serializer as the load endpoint, send failures to the same error queue, and handle only smoke messages. Their input addresses come from the transport (`ReceiveAddresses.MainReceiveAddress`), so SQL Server schema and catalog are included. Stopping a host, which a check does explicitly for the heartbeat and always at the end (before any API cleanup, so an unreachable ServiceControl cannot delay it), stops that endpoint and its plugins; it does not stop the tool or the load endpoint. It also does not delete the queues or tables the endpoint created: those run-scoped transport entities stay until the broker or database is torn down. The check exercises the NServiceBus.Heartbeat and NServiceBus.Metrics.ServiceControl plugins.
- The saga is a real saga in the tool endpoint with the NServiceBus.SagaAudit plugin and learning persistence in the temp folder, not a snapshot written by hand.
- Rows marked manual are not automated: instance installation and upgrade, how ServicePulse renders custom checks, heartbeats, recoverability screens, search, the sequence diagram, the saga view and the monitoring graphs, the public integration events sample, chaos testing, and performance, load and stability testing. The API data behind the rendered views is verified by the automated rows above.

API:

- `GET /api/smoke` — the current or last run, or the idle checklist
- `POST /api/smoke/start` — start a run; 400 when one is running or a conflicting job is running
- `POST /api/smoke/stop` — cancel the current run

## Layout

```
tools/testing-tool/
  TestingTool.slnx
  Dockerfile                     # multi-stage container build (build context = repo root)
  TestingTool/                   # ASP.NET Core host — API endpoints, web UI, NServiceBus endpoint,
                                 #   scenario runner, bypass writer, recoverability/search jobs,
                                 #   smoke test runner and checks (Smoke/),
                                 #   OTel setup, ServiceControl REST client
  TestingTool.Scenarios/         # IScenario contract + the six scenario implementations
  TestingTool.Contracts/         # shared DTOs (scenarios, status, jobs, bypass)
  TestingTool.Tests/             # Unit tests for the tool itself
  TestingTool.AppHost/           # Aspire AppHost — orchestrates platform, tool, and observability stack
    obs/                         # observability config: OTel Collector, Prometheus, Grafana provisioning
                                 #   + prebuilt dashboard
```

## Run locally

```bash
dotnet build tools/testing-tool/TestingTool.slnx --configuration Release
dotnet run --project tools/testing-tool/TestingTool --configuration Release
```

Open http://localhost:5290 (or the port shown in the console).

## Run in a container

The tool ships a multi-stage Dockerfile that uses the same chiseled base image as the ServiceControl
containers. The build context is the repository root (so `global.json` and `nuget.config` are
available):

```bash
docker build -f tools/testing-tool/Dockerfile -t particular/testing-tool .
docker run --rm -p 8080:8080 \
  -e TestingTool__ServiceControlApiUrl=http://host.docker.internal:33333 \
  -e ConnectionStrings__transport=amqp://guest:guest@host.docker.internal:5672 \
  particular/testing-tool
```

Set `ConnectionStrings__transport` to the same broker ServiceControl uses. If it's not set, the tool
uses the Learning transport and nothing reaches ServiceControl (see [Transport](#transport)).

The container listens on port 8080. CI ([testing-tool-ci.yml](/.github/workflows/testing-tool-ci.yml))
builds the solution and the container image on every change under `tools/testing-tool/`.

## Run with Aspire

The Aspire AppHost orchestrates the testing tool together with the full Particular platform
(ServiceControl + RabbitMQ or SQL Server transport + RavenDB/SQL Server/PostgreSQL storage + ServicePulse) and a complete observability
stack (OTel Collector, Jaeger, Prometheus, Grafana), so a single command brings up the whole
system locally:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj
```

To test a specific ServiceControl image tag (e.g. a PR-based prerelease tag):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234
```

To select a storage backend for the ServiceControl error instance (`RavenDb`,
`SqlServer`, or `PostgreSql`; defaults to `RavenDb`):

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --storage:RavenDb
```

`--storage RavenDb` (space separator) is accepted too. Both flags may be combined:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- --tag pr-1234 --storage:SqlServer
```

### AppHost CLI options

All flags are passed after `--` to the AppHost. Each accepts either `--name value` (space
separator) or `--name:value` (colon separator):

| Flag | Default | Values | Description |
|---|---|---|---|
| `--storage` | `RavenDb` | `RavenDb`, `SqlServer`, `PostgreSql` | Storage backend for the ServiceControl error instance and for the audit instances |
| `--transport` | `RabbitMq` | `RabbitMq`, `SqlServer` | Transport shared by ServiceControl and the testing tool. `SqlServer` reuses the storage SQL Server container with a separate `Transport` database |
| `--tag` | *(none — uses the current build's image)* | any image tag, e.g. `pr-1234` or `6.3.1` | Override the ServiceControl container image tag (useful for testing PR-based prereleases) |
| `--error-ingestion-scale-unit` | `0` | non-negative integer | Number of additional error-ingestion-only scale-out instances to spin up alongside the primary error instance (each runs with `--error-ingestion-only`) |
| `--audit-instances` | `0` | non-negative integer | Number of ServiceControl.Audit instances to add. They take the same `--storage` backend, report custom checks to the primary and export OTel to the collector |
| `--azure-blob-body-storage` | *(none — uses file-system body storage)* | `"<connection string>"` | Store message bodies in Azure Blob Storage instead of the container file system. Applies only with `--storage:SqlServer` or `--storage:PostgreSql`. Quote the value because connection strings contain semicolons. The error instance and its scale-out units share the `error-bodies` container, and each audit instance gets its own `audit-bodies-<instance name>` container |
| `--s3-body-storage` | *(none — uses file-system body storage)* | `<bucket name>` | Store message bodies in an S3 bucket instead of the container file system. Applies only with `--storage:SqlServer` or `--storage:PostgreSql`, and cannot be combined with `--azure-blob-body-storage`. Setup creates the bucket if it is missing and the credentials permit it. The error instance and its scale-out units share the `error-bodies/` key prefix, and each audit instance gets its own `audit-bodies-<instance name>/` prefix |
| `--s3-region` | SDK default | region name, e.g. `eu-west-1` | Region for `--s3-body-storage`. Requires `--s3-body-storage` |
| `--s3-service-url` | AWS | absolute `http` or `https` URL | S3-compatible endpoint, e.g. MinIO or LocalStack. The ServiceControl containers connect to it, so it must be reachable from inside a container. Requires `--s3-body-storage` |
| `--s3-access-key-id`, `--s3-secret-access-key` | *(none — default AWS credential chain)* | key pair | Static credentials for `--s3-body-storage`. Set both or neither. Without them, the AWS default credential chain runs inside each container; the host's credentials are not forwarded. Static keys appear in the container environment |

Pass static credentials explicitly, or omit both key options when the containers can use the AWS default credential chain:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- \
  --storage PostgreSql --s3-body-storage my-bodies --s3-region eu-west-1 \
  --s3-access-key-id "$AWS_ACCESS_KEY_ID" --s3-secret-access-key "$AWS_SECRET_ACCESS_KEY"
```

For MinIO or LocalStack, use an endpoint reachable from the containers. This example assumes `host.docker.internal` resolves to the host:

```bash
aspire run tools/testing-tool/TestingTool.AppHost/TestingTool.AppHost.csproj -- \
  --storage SqlServer --s3-body-storage bodies --s3-region us-east-1 \
  --s3-service-url http://host.docker.internal:9000 \
  --s3-access-key-id minioadmin --s3-secret-access-key minioadmin
```

The static credential options do not support session tokens. For temporary AWS credentials, configure the default credential chain inside the containers instead.

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

## Horizontal scaling

Load counters, job controls, and smoke-run results are in-memory per replica. The smoke saga uses learning persistence in a local temporary directory. Run a single instance with `dotnet run` or the Aspire AppHost. For multiple replicas, provide orchestration and a distinct shard id for each replica so deterministic failure decisions do not overlap:

| Shard id source | When |
|---|---|
| `SHARD_ID` env var | Explicit override — recommended for any custom deployment |
| Hostname trailing ordinal (e.g. `testing-tool-2` → `2`) | StatefulSet-style ordered hostnames |
| `MachineName` | Fallback — unique per host/pod |

To achieve a target aggregate rate of R msg/s across N replicas, set each replica's scenario rate
to R/N. The web UI and `/api/status` endpoint report per-replica counters; aggregate across
replicas via Prometheus queries or the OTLP backend.

## Transport

The endpoint's transport is picked from the `transport` connection string
(`ConnectionStrings__transport`). The Aspire AppHost sets this automatically; when running the tool
on its own, set it to the broker your ServiceControl instance uses:

| Connection string | Transport |
|---|---|
| starts with `amqp:` or `amqps:` (e.g. `amqp://guest:guest@localhost:5672`) | RabbitMQ — quorum queues, conventional routing topology |
| contains an `Endpoint=sb://...` entry (e.g. `Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...`) | Azure Service Bus — default topic topology. The direct error and audit writers send through the same endpoint, so they use it too |
| SQL Server connection string with `Initial Catalog` or `Database` (e.g. `Server=localhost;Database=Transport;...`) | SQL Server |
| not set or empty | Learning transport (local standalone runs only — ServiceControl won't see the messages) |
| anything else | startup fails with an error naming `ConnectionStrings__transport` |

The selected transport is logged at startup (`Using RabbitMQ transport` etc.). The smoke tests start their own run-scoped endpoints with the same selection, so they need the same `ConnectionStrings__transport`.

The Standard Service Bus tier limits a message to 256 KiB including headers. The `large-messages` scenario serializes to about 190 KiB, so it leaves room for headers on Standard and still exceeds the 100 KB external body storage threshold.

## Configuration

All configuration is via environment variables (no files, no database). Settings are in
`appsettings.json` under the `TestingTool` section, overridable by environment variables using
`__` as the section separator (e.g. `TestingTool__ServiceControlApiUrl`):

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings__transport` (env) | *(none — Learning transport)* | Transport connection string; see [Transport](#transport) |
| `TestingTool__ServiceControlApiUrl` | `http://localhost:33333` | ServiceControl REST API base URL |
| `TestingTool__ReplayInterval` | `00:02:00` | Default interval for the retry job |
| `TestingTool__ReplayMinGroupSize` | `1` | Min messages in a group before retrying |
| `TestingTool__SearchInterval` | `00:01:00` | Default interval for the search job |
| `TestingTool__ArchiveInterval` | `00:02:00` | Default interval for the archive job |
| `TestingTool__ArchiveMinGroupSize` | `1` | Min messages in a group before archiving |
| `TestingTool__RetentionSweepInterval` | `00:05:00` | Default interval for the retention-sweep job |
| `TestingTool__ServiceControlInputQueue` | `Particular.ServiceControl` | ServiceControl error instance input queue (custom-check reports destination) |
| `TestingTool__CustomCheckInterval` | `00:00:30` | Default interval for the custom-check-failures job |
| `TestingTool__CustomCheckHost` | `ServiceControl` | `Host` field on injected custom-check reports |
| `TestingTool__CustomCheckFailureProbability` | `0.4` | Probability (0–1) a given check is reported failed each cycle |
| `TestingTool__AuditQueueName` | `audit` | NServiceBus audit queue the ServiceControl.Audit instance drains |
| `TestingTool__AuditSagaSnapshotRatio` | `0.2` | Default saga snapshots per processed message for the audit-queue writer |
| `TestingTool__MonitoringQueueName` | `Particular.Monitoring` | ServiceControl monitoring instance queue that endpoint metrics are sent to |
| `TestingTool__MonitoringApiUrl` | `http://localhost:33633` | ServiceControl.Monitoring API base URL, used by the smoke tests |
| `TestingTool__SmokePollInterval` | `00:00:01` | Interval between polls while a smoke check waits for an effect |
| `TestingTool__SmokePollTimeout` | `00:01:00` | How long a smoke check waits for one effect before it fails |
| `TestingTool__SmokeCheckTimeout` | `00:03:00` | How long one smoke check may take in total |
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

## Background

The tool was designed with reference to two prior load-generation tools:

- [ServiceControlFeeder](https://github.com/dvdstelt/ServiceControlFeeder) — writes raw failed-message envelopes straight to the ServiceControl error queue; the pattern behind the direct bypass writer
- [FakeMessageGen](https://github.com/ramonsmits/FakeMessageGen) — high-throughput, rate-controlled fake message generation
