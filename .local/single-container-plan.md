# Implementation plan: single ServiceControl container image with roles

Source recommendations: [`.local/single-container-notes.md`](single-container-notes.md)  
Finalized design contract: [`docs/single-container-design.md`](../docs/single-container-design.md)  
Target issue: Particular/ServiceControl#5028

## Plan maintenance instructions

- Check off each checklist item (`[x]`) as soon as its work is completed and verified.
- Link every document created while implementing this plan from this plan, using a repository-relative Markdown link.
- Add document links near the plan header when they apply to the overall design; otherwise add them beside the relevant phase or checklist item.
- Keep incomplete or partially implemented work unchecked, and note partial progress beneath the relevant item when useful.

## Goal

Ship one canonical ServiceControl application image that contains the existing Primary, Audit, and Monitoring applications and uses a .NET PID-1 launcher to select one or more roles. Preserve each role as an independent child process with its existing command parser, DI container, NServiceBus endpoint, HTTP port, persistence lifecycle, and deployment artifact.

Deliver this incrementally:

1. Run the unified image once per role as a drop-in replacement for the three current images.
2. Support `Primary,Audit,Monitoring` and `All` in one container by supervising the same isolated applications.
3. Defer a true single-process/single-web-host design until footprint measurements justify the architectural work.

## High-level implementation checklist

- [x] Finalize and document the role-selection, compatibility, command, failure, shutdown, and publishing contracts.
- [x] Add the .NET launcher and tests for role parsing, argument forwarding, and ServicePulse capability handling.
- [x] Implement child-process startup, supervision, signal forwarding, graceful shutdown, and failure propagation.
- [ ] Add fail-fast validation for unsafe combined-role configuration while preserving existing per-role validation.
- [ ] Build one canonical image containing isolated Primary, Audit, Monitoring, and launcher artifacts.
- [ ] Replace the standalone health-check application with launcher-owned aggregate health checks.
- [ ] Prove the unified image as a drop-in replacement with one role per container across the existing transport matrix.
- [ ] Add and verify the combined `All` topology, including role APIs, ServicePulse, routing, persistence isolation, and process failure behavior.
- [ ] Update build, publish, cleanup, and integration-test workflows; decide and implement the legacy-image compatibility period.
- [ ] Update container documentation, deployment guidance, migration examples, and synchronized external samples.
- [ ] Complete unit, package, multi-architecture image, container, shutdown, crash, and upgrade verification.
- [ ] Roll out the canonical image, retain old publishing paths until compatibility is proven, and record follow-up footprint measurements.

## Scope and non-goals

### In scope

- One canonical Linux image containing all three application artifacts.
- A role contract based on `SERVICE_CONTROL_ROLE`.
- A .NET launcher that acts as PID 1 and supervises child processes.
- Role-aware aggregate health.
- Fail-fast validation of role selection and high-risk combined-role configuration.
- CI, integration tests, release publishing, deployment samples, and documentation.
- A migration path from the current role-specific repositories.

### Not in scope

- Combining `AddServiceControl`, `AddServiceControlAudit`, and `AddServiceControlMonitoring` in one service provider.
- Changing Windows installer ZIPs, executable entrypoints, or installer packaging.
- Flattening deployment artifacts into one directory.
- Embedding RavenDB into the application image; `ServiceControl.RavenDB` remains separate.
- Reworking API routes or exposing all roles through one HTTP port.
- Reducing the three selected roles to one OS process.

## Product decisions to record before coding

Document these decisions in the issue and in the container README. The recommended answers below should become tests.

| Question | Recommended contract |
|---|---|
| Variable name | `SERVICE_CONTROL_ROLE` |
| Default when omitted | `Primary`, preserving the current `particular/servicecontrol` behavior |
| Parsing | Case-insensitive, trim whitespace, accept comma-separated values, remove duplicates |
| Canonical roles | `Primary`, `Audit`, `Monitoring`, `ServicePulse`, `All` |
| `ServicePulse` | A capability, not a child process; it implies `Primary` |
| `All` | Expands to `Primary,Audit,Monitoring,ServicePulse` |
| Existing integrated ServicePulse variable | Continue supporting `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE`; reject an explicit `false` when the selected role set requires `ServicePulse` rather than silently overriding it |
| Role start order | Start in canonical order: Primary, Audit, Monitoring; do not treat ordering as a readiness dependency |
| Multi-role commands | Initially allow normal run and `--setup-and-run`; reject maintenance/import/help/setup commands with multiple process roles and explain that they must be run one role at a time |
| Failure policy | Any unexpected child exit stops all remaining children and makes the container exit non-zero |
| Shutdown policy | Forward termination to all children, wait for the configured launcher grace period, then kill remaining process trees |
| Ports | Preserve 33333 (Primary), 44444 (Audit), and 33633 (Monitoring) |
| Old image repositories | Recommended: publish thin compatibility wrapper images for one major-version transition, each setting a role-specific default, then retire them. A plain OCI alias is insufficient because aliases cannot inject a different role default. |

## Proposed code structure

Add a small executable and test project:

```text
src/Platform.Launcher/
  Platform.Launcher.csproj
  Program.cs
  ContainerRole.cs
  RoleSelection.cs
  RoleDescriptor.cs
  ContainerCommand.cs
  ConfigurationValidator.cs
  ChildProcessSupervisor.cs
  HealthCheck.cs
  UnixSignal.cs

src/Platform.Launcher.UnitTests/
  Platform.Launcher.UnitTests.csproj
  RoleSelectionTests.cs
  ContainerCommandTests.cs
  ConfigurationValidatorTests.cs
  ChildProcessSupervisorTests.cs
  HealthCheckTests.cs
  Fakes/...
```

`Platform.Launcher` should be the only new OCI entrypoint and should support two modes:

- default / `run`: validate and supervise selected role applications;
- `health`: parse the same role selection and probe every selected role endpoint.

Using one executable for supervision and health avoids duplicating role parsing between the launcher and `HealthCheckApp`. After migration, remove `src/HealthCheckApp` if no other packaging path uses it.

Add both projects to `src/ServiceControl.slnx`, placing the executable under `/Instances/Shared/` and tests under `/Instances/Shared/Testing/`.

## Phase 1: role model and launcher command contract

### 1.1 Implement role parsing

In `RoleSelection.cs`:

- Read `SERVICE_CONTROL_ROLE`, defaulting to `Primary` only when absent or empty according to the agreed contract.
- Split on commas, trim entries, compare using `OrdinalIgnoreCase`, and deduplicate.
- Reject empty list elements only if the team wants strict parsing; otherwise ignore whitespace-only elements consistently.
- Expand `All` and `ServicePulse` before validation.
- Return process roles separately from capabilities, e.g. process roles `{Primary, Audit}` and capability `{ServicePulse}`.
- Emit canonical role names in diagnostics and health output.
- Reject unknown values with the complete allowed-value list.

Define immutable `RoleDescriptor` data for each process role:

| Role | Executable | Working directory | Health endpoint | Port |
|---|---|---|---|---:|
| Primary | `/app/primary/ServiceControl` | `/app/primary` | `http://localhost:33333/api/configuration` | 33333 |
| Audit | `/app/audit/ServiceControl.Audit` | `/app/audit` | `http://localhost:44444/api/configuration` | 44444 |
| Monitoring | `/app/monitoring/ServiceControl.Monitoring` | `/app/monitoring` | `http://localhost:33633/connection` | 33633 |

Allow the application root to be injected in tests instead of hard-coding `/app` throughout the implementation.

### 1.2 Parse launcher versus child arguments

`Program.cs` should consume only launcher-owned modes/options and pass all existing application arguments unchanged to each selected child. Do not move the current `Program.cs` bodies into the launcher and do not reference the three application projects.

Rules:

- With one process role, pass all arguments through unchanged. This preserves setup, maintenance, import, and help behavior.
- With multiple process roles, permit only no arguments (normal run) and `--setup-and-run` initially.
- Return a distinct usage/configuration exit code for invalid role or command combinations.
- Ensure `SERVICE_CONTROL_ROLE` remains in the environment inherited by child and `IntegratedSetup.Run` re-executed processes.
- Print a startup summary containing selected process roles, capabilities, child paths, and ports, but never connection strings, certificates, or tokens.

### 1.3 ServicePulse capability handling

Before starting Primary:

- If the role set includes the `ServicePulse` capability and `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE` is unset, add `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE=true` to the Primary child environment.
- If it is explicitly false, fail with an actionable conflict message.
- Do not start a fourth process.
- Preserve package-owned `MONITORING_URL`/`MONITORING_URLS` values. Documentation must emphasize that these are browser-visible URLs and normally cannot use container-local `localhost` when users access ServicePulse externally.

## Phase 2: process supervision and shutdown

Implement `ChildProcessSupervisor` behind small interfaces for process creation, time, and signal delivery so behavior can be unit tested without launching ServiceControl.

### Startup

- Verify every selected executable exists before launching any child.
- Use `ProcessStartInfo` with the role-specific working directory and original arguments.
- Do not use a shell; the chiseled runtime does not guarantee one.
- Inherit the launcher environment, applying only capability-specific child overrides.
- Redirect stdout/stderr and attribute each complete line with the canonical role, while keeping stdout and stderr separate. If structured JSON logs must remain byte-for-byte valid, instead inherit output unchanged and emit only role-labelled launcher lifecycle events; make this choice explicitly before implementation.
- Start children in canonical order and begin observing all exit tasks immediately.

### Runtime failure policy

- If any child exits before cancellation, record its role and exit code.
- Initiate shutdown of every other child.
- Exit with the failed child’s non-zero code when usable; otherwise use a stable launcher failure code.
- If a child exits zero while siblings are still expected to run, still treat it as an unexpected termination and stop the container; a multi-role container must not continue partially healthy.
- Guard against races where multiple children exit together and select the first observed failure deterministically for diagnostics.

### Signal handling

- Register SIGTERM and SIGINT using .NET Unix/POSIX signal APIs and also handle `Console.CancelKeyPress` for local execution.
- Forward the same graceful termination signal to every direct child. Since `Process.Kill` is not graceful, isolate native signal delivery in `UnixSignal.cs` rather than using a shell command.
- Wait for all roles within a launcher shutdown grace period. Default it so the role applications retain time inside the platform’s stop timeout; expose a launcher-specific environment variable if configurability is required.
- After the grace period, call `Kill(entireProcessTree: true)` for remaining children so an `IntegratedSetup.Run` subprocess cannot be orphaned.
- Dispose process and signal registrations and return only after output pumps complete.

Add an integration-style unit test fixture using tiny fake child executables or the existing `SetupProcessFake` pattern to verify argument forwarding, output, exit propagation, sibling shutdown, and timeout escalation.

## Phase 3: combined-role configuration validation

Keep normal role-owned validation in the existing applications. The launcher should add only cross-role checks that an individual process cannot perform.

### 3.1 Reuse environment precedence

The current precedence is implemented by the internal `EnvironmentVariableSettingsReader` in `src/ServiceControl.Configuration/EnvironmentVariableSettingsReader.cs`. Avoid independently reimplementing prefix normalization and fallback rules.

Recommended approach:

- Add `InternalsVisibleTo` for `Platform.Launcher` in `ServiceControl.Configuration.csproj` and reference that project from the launcher.
- Use `EnvironmentVariableSettingsReader` with `SettingsRootNamespace("ServiceControl")`, `("ServiceControl.Audit")`, `("Monitoring")`, and `("ServiceBus")`.
- Keep the launcher container-only; it does not need registry/config-file resolution.
- Centralize setting names in the validator and test namespaced-over-unprefixed precedence.

Do not instantiate the three application `Settings` classes in the launcher. That would load application dependencies and repeat process-global logging/configuration behavior the launcher is intended to isolate.

### 3.2 Required startup checks

For multiple selected process roles, fail before starting any child when:

1. Resolved `InstanceName` values collide, using current defaults when unset:
   - `Particular.ServiceControl`;
   - `Particular.ServiceControl.Audit`;
   - `Particular.Monitoring`.
2. Resolved `TransportType` values differ or a required value is absent.
3. Resolved transport `ConnectionString` values differ where they are directly comparable, or required values are absent. Do not print secret values.
4. Primary’s `SERVICEBUS_ERRORQUEUE` differs from Monitoring’s `MONITORING_ERRORQUEUE` after defaults are applied.
5. Audit’s `ServiceControlQueueAddress`, when supplied, conflicts with Primary’s resolved instance name.
6. Primary and Audit resolve to the same Raven database name, embedded `DbPath`, or database maintenance port.
7. Authentication authority, audience, authorization switch, and claim-name settings differ across selected HTTP APIs after fallback resolution.
8. Selected role descriptors contain duplicate listener ports.
9. `ServicePulse` capability conflicts with an explicitly disabled integrated ServicePulse setting.
10. Arguments request an unsupported multi-role command.

Validation messages must name roles and setting keys, explain the required relationship, and avoid values for connection strings/certificates. Unit tests should cover shared unprefixed values, namespaced overrides, defaults, conflicts, and redaction.

Some persistence providers have provider-specific settings and aliases. Implement Raven identity checks first, because RavenDB is the container default, then add equivalent provider-specific checks only where shared ownership is actually unsafe. Do not block valid external Raven configurations merely because Primary and Audit share the same server URL; only storage identity must differ.

## Phase 4: unified image

Replace the application content of `src/ServiceControl/Dockerfile` with the canonical unified build while leaving `src/ServiceControl.RavenDB/Dockerfile` unchanged.

### Build stage

- Build these projects for `$TARGETARCH` in Release:
  - `src/ServiceControl/ServiceControl.csproj`;
  - `src/ServiceControl.Audit/ServiceControl.Audit.csproj`;
  - `src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj`.
- Publish `src/Platform.Launcher/Platform.Launcher.csproj` to a dedicated launcher directory.
- Continue relying on each application’s existing `Artifact` items and imported `ProjectReferences.Transports.props` / persister props so transport and persistence plugins are copied exactly as today.
- Add a build-time assertion that all three artifact directories and their executables exist.

### Runtime stage

Use isolated directories:

```text
/app/launcher/
/app/primary/
/app/audit/
/app/monitoring/
```

- Copy `/deploy/Particular.ServiceControl` to `/app/primary`.
- Copy `/deploy/Particular.ServiceControl.Audit` to `/app/audit`.
- Copy `/deploy/Particular.ServiceControl.Monitoring` to `/app/monitoring`.
- Never flatten these directories.
- Expose 33333, 44444, and 33633.
- Set `ENTRYPOINT ["/app/launcher/Platform.Launcher"]`.
- Set health to `HEALTHCHECK --start-period=10s CMD ["/app/launcher/Platform.Launcher", "health"]`.
- Retain `USER $APP_UID`.

Replace unsafe unprefixed image defaults with namespaced equivalents:

```text
SERVICECONTROL_PERSISTENCETYPE=RavenDB
SERVICECONTROL_FORWARDERRORMESSAGES=false
SERVICECONTROL_ERRORRETENTIONPERIOD=15
SERVICECONTROL_AUDIT_PERSISTENCETYPE=RavenDB
SERVICECONTROL_AUDIT_AUDITRETENTIONPERIOD=7
```

Confirm exact environment key normalization in tests. Do not set unprefixed `PersistenceType`, `AuditRetentionPeriod`, `RAVENDB_*`, `DBPATH`, or maintenance-port defaults in the unified image.

After the canonical image works, either remove the Audit and Monitoring Dockerfiles or convert them into explicitly temporary compatibility wrappers according to the release decision. Do not continue three full independent builds.

## Phase 5: aggregate health

Implement the launcher’s `health` mode with no dependency on the role child processes or application assemblies.

- Parse and expand `SERVICE_CONTROL_ROLE` using the same `RoleSelection` implementation as run mode.
- Probe every selected process role’s descriptor URL concurrently with a short per-request timeout.
- Require HTTP success, `application/json`, and non-empty content, matching the current `HealthCheckApp` behavior.
- Ignore unselected roles and treat ServicePulse as covered by Primary’s endpoint; optionally add a Primary-root probe only if it provides a stable non-redirecting health contract.
- Print one line per role and a final aggregate result.
- Return zero only when all selected role endpoints pass.
- Redact response bodies and configuration values from errors.

Child liveness is covered by supervision: if a child exits, PID 1 exits and the container stops. Endpoint probes therefore cover readiness/functional liveness without requiring the separate health process to inspect the supervisor’s in-memory state.

Tests should use an in-process HTTP server to cover one role, all roles, timeout, non-success, invalid content type, empty body, and partial failure with the failed role named.

## Phase 6: container integration tests

Update `src/container-integration-test` in two steps so regressions can be localized.

### 6.1 Unified image, separate containers

Change all three services in `servicecontrol.yml` to use `ghcr.io/particular/servicecontrol:${SERVICECONTROL_TAG}` and set:

- `SERVICE_CONTROL_ROLE=Primary` on `servicecontrol`;
- `SERVICE_CONTROL_ROLE=Audit` on `servicecontrol-audit`;
- `SERVICE_CONTROL_ROLE=Monitoring` on `servicecontrol-monitoring`.

Keep current ports, names, dependencies, transport overlays, and expected healthy-container counts. This proves the image can replace each legacy image without changing topology.

### 6.2 Combined container profile

Add a second compose file/profile, for example `src/container-integration-test/combined.yml`, that:

- replaces the three application services with one service using `SERVICE_CONTROL_ROLE=All`;
- publishes all three ports;
- supplies explicit namespaced instance and persistence identity values;
- points Primary’s remote Audit URL at `http://localhost:44444/api` for server-side aggregation;
- sets Audit’s Primary queue address to Primary’s resolved endpoint name;
- sets Primary and Monitoring to the same error queue;
- uses one external RavenDB server but different Primary/Audit databases;
- configures a browser-valid Monitoring URL when integrated ServicePulse is tested.

Extend `.github/workflows/container-integration-test.yml` to run both topologies across the transport matrix, or run the combined profile on a representative transport first if CI cost is too high. Update expected healthy counts and log dumping for the combined container.

Do more than container-count health for the combined profile. Add scripted assertions for:

- all three role health endpoints;
- Primary configuration/API availability;
- Audit API availability;
- Monitoring connection API availability;
- integrated ServicePulse root availability;
- Primary-to-Audit aggregation;
- Audit-to-Primary custom-check/queue routing where practical;
- actual Monitoring consumption from Primary’s error queue.

Preserve diagnostic dumps on failure, including the combined launcher output and all dependent containers.

## Phase 7: CI and release publishing

### Build workflow

Update `.github/workflows/build-containers.yml`:

- remove the three-application matrix;
- build one `servicecontrol` application image from `src/ServiceControl/Dockerfile`;
- keep the RavenDB build path separate wherever it is currently invoked;
- update OCI title/description to describe Primary, Audit, Monitoring, and integrated ServicePulse roles;
- continue multi-arch `linux/amd64,linux/arm64`, SBOM, and current labels.

Validate workflow changes with `actionlint` because these files are GitHub Actions workflows.

### Push workflow

Update `.github/workflows/push-container-images.yml`:

- publish the canonical `particular/servicecontrol` and unchanged `particular/servicecontrol-ravendb` repositories;
- update the canonical Docker Hub description from `src/ServiceControl/Container-README.md`;
- if compatibility wrappers are approved, publish them from explicit wrapper manifests and mark their READMEs deprecated with migration examples;
- otherwise remove Audit/Monitoring repositories from the publish loop and call out the breaking repository change in release notes.

### Cleanup workflow

Update `.github/workflows/clean-ghcr.yml` to clean the canonical and RavenDB repositories, plus temporary compatibility repositories only for as long as they are published.

## Phase 8: documentation and synchronized samples

Update:

- `src/ServiceControl/Container-README.md` to document role syntax, defaults, examples, ports, ServicePulse semantics, namespaced configuration, health/failure behavior, and separate-vs-combined deployment examples.
- `src/ServiceControl.Audit/Container-README.md` and `src/ServiceControl.Monitoring/Container-README.md` only if compatibility wrappers remain; otherwise replace/remove them as part of repository retirement.
- `docs/deployment.md` to list one application image plus RavenDB and explain migration.
- `src/container-integration-test/README.md` for both topologies and revised healthy counts.
- `docs/test-ghcr-tag/compose.yml` to use the canonical image and explicit roles.
- External synchronized examples named in `src/container-integration-test/servicecontrol.yml`:
  - `Particular/PlatformContainerExamples` Docker Compose samples;
  - `ParticularLabs/AwsLoanBrokerSample/docker-compose.yml`.

Documentation must distinguish:

- shared transport/auth values from role-specific identity/persistence/retention values;
- server-side loopback URLs from browser-visible ServicePulse URLs;
- one image used in three containers from `All` in one container;
- one container from one process—the combined mode intentionally still runs three role processes.

Provide an upgrade table:

| Old image | New image | Required role |
|---|---|---|
| `particular/servicecontrol` | `particular/servicecontrol` | omitted or `Primary` |
| `particular/servicecontrol-audit` | `particular/servicecontrol` | `Audit` |
| `particular/servicecontrol-monitoring` | `particular/servicecontrol` | `Monitoring` |

State that existing volumes/databases/queues remain unchanged when role-specific settings are preserved.

## Verification matrix

### Unit tests

- Role parsing: default, casing, whitespace, duplicates, `All`, ServicePulse implication, unknown values, conflicts.
- Argument policy: complete single-role pass-through and rejected multi-role commands.
- Configuration precedence and every cross-role validation rule.
- Secret redaction in validation and process diagnostics.
- Child lifecycle: startup, argument/environment forwarding, one-child compatibility, failure propagation, sibling shutdown, signal forwarding, escalation, simultaneous exits.
- Health: selected-only probes and aggregate failure diagnostics.

### Build/package tests

- `dotnet build src/ServiceControl.slnx --configuration Release`.
- Launcher unit tests.
- Docker build for amd64 and arm64 in CI.
- Inspect image contents to ensure three isolated artifact trees contain expected transport and persister plugins.
- Confirm old installer ZIP/package outputs are byte-equivalent except for unrelated build metadata; the launcher must not enter installer packaging.

### Container behavior

- Each role alone with omitted/explicit selection as applicable.
- Unified image in three containers across every existing transport.
- `All` in one container.
- `Primary,Monitoring`, `Primary,Audit`, and a reordered/duplicated input.
- Integrated ServicePulse enabled through both role capability and legacy variable.
- Invalid role, invalid multi-role command, duplicate identity, transport mismatch, persistence collision, and auth mismatch.
- SIGTERM during normal run and during `--setup-and-run`; no orphan processes and exit within platform timeout.
- Deliberate child crash; siblings stop and container exits non-zero.
- Upgrade from each old image to equivalent canonical role using existing storage.

## Suggested pull request sequence

1. **Role contract and launcher core** — new projects, parser, descriptors, argument policy, supervisor, unit tests; no publishing changes.
2. **Unified image and health** — canonical Dockerfile, namespaced defaults, aggregate health, one-role image tests.
3. **Combined-role validation and topology** — cross-role validator, combined compose profile, signal/failure integration tests.
4. **CI/release migration** — build/push/cleanup workflows and compatibility-wrapper decision.
5. **Documentation and external sample synchronization** — migration guide, deployment examples, release notes.

Keep each PR independently deployable where possible. Do not remove old publishing paths until the canonical image has passed the existing separate-container transport matrix.

## Exit criteria

The implementation is complete when:

- one canonical application image contains and can launch all three existing role artifacts;
- the same image passes the existing transport matrix with one role per container;
- `SERVICE_CONTROL_ROLE=All` starts Primary, Audit, Monitoring, and integrated ServicePulse in one container;
- health fails if any selected endpoint fails and names the role;
- an unexpected role-process exit tears down the whole container with a non-zero code;
- SIGTERM gracefully reaches all children without leaving setup descendants;
- dangerous cross-role configuration collisions fail before processes start;
- RavenDB remains a separate image and installer/package artifacts are unaffected;
- release workflows publish the agreed canonical/compatibility repositories;
- deployment docs clearly describe role selection, configuration boundaries, migration, and the retained multi-process architecture.

## Deferred follow-up: true integrated host

Only open the single-process effort after measuring image size, RSS, startup time, and operational complexity of the supervised design. That follow-up requires a separate architecture plan covering keyed NServiceBus endpoints and endpoint-specific services, one web-policy owner, removal or replacement of colliding controllers, local Audit/Monitoring façades, persistence ownership (especially embedded RavenDB), global logging, and cross-role failure semantics. It must not be implemented by simply calling the three existing host extension methods on one `WebApplicationBuilder`.
