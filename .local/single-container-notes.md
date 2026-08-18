# Single ServiceControl container / entrypoint research

Research target: [Particular/ServiceControl#5028](https://github.com/Particular/ServiceControl/issues/5028), “Deploy only one ServiceControl container image with roles support” (opened 2025-06-30).

The issue asks for one image containing Primary/Error, Audit, Monitoring, and optionally ServicePulse, selected with a role variable such as `SERVICE_CONTROL_ROLE=Audit`, `Primary,Monitoring`, or `All`.

## Executive summary

There are two separate goals hidden in the issue:

1. **One distributable image and one container entrypoint** — comparatively small and low risk.
2. **One runtime host for multiple roles** — a substantial architecture change.

The recommended path is incremental:

1. Build one image containing the existing three application artifacts in isolated directories.
2. Add one container launcher/entrypoint that parses roles and starts the selected application(s).
3. Initially preserve each role’s process, DI container, HTTP port, endpoint, persistence lifecycle, and command parser.
4. Treat ServicePulse as a capability of Primary using the existing integrated ServicePulse support.
5. Only pursue a true single-process/single-host implementation later, if measurements show that reducing container count without reducing process count is insufficient.

Calling `AddServiceControl`, `AddServiceControlAudit`, and `AddServiceControlMonitoring` on one `WebApplicationBuilder` is **not viable as-is**. The roles have unkeyed endpoint-specific DI services, unkeyed NServiceBus endpoint registration, colliding HTTP routes, independently configured MVC/auth/CORS/logging, and independently owned persistence. Integrated ServicePulse is not an equivalent precedent: it is static UI/middleware added to Primary, whereas Audit and Monitoring are active NServiceBus endpoints with hosted services and (for Audit) persistence.

A single image with one role per container directly addresses image proliferation. A launcher supervising multiple existing processes is the lowest-risk way to add `Primary,Audit,Monitoring` and `All`. It reduces topology/container count, but not process memory to the same degree as true in-process embedding.

## Current runtime shape

There are three independent ASP.NET Core executables:

| Role | Entrypoint | Normal host construction | Container port |
|---|---|---|---:|
| Primary/Error | `src/ServiceControl/Program.cs` | `src/ServiceControl/Hosting/Commands/RunCommand.cs` | 33333 |
| Audit | `src/ServiceControl.Audit/Program.cs` | `src/ServiceControl.Audit/Infrastructure/Hosting/Commands/RunCommand.cs` | 44444 |
| Monitoring | `src/ServiceControl.Monitoring/Program.cs` | `src/ServiceControl.Monitoring/Hosting/Commands/RunCommand.cs` | 33633 |

All three programs independently:

- populate legacy executable configuration;
- construct role-namespaced logging settings;
- configure process-global logging;
- run the `--setup-and-run` workaround in `src/ServiceControl.Infrastructure/IntegratedSetup.cs`;
- parse a role-specific command set;
- construct role-specific settings;
- dispatch to a role-specific internal `CommandRunner`.

The run commands independently create, build, and run a `WebApplication`. There is no role dispatcher today.

The role registration seams are already reasonably clear:

- Primary: `AddServiceControl` in `src/ServiceControl/HostApplicationBuilderExtensions.cs`.
- Audit: `AddServiceControlAudit` in `src/ServiceControl.Audit/HostApplicationBuilderExtensions.cs`.
- Monitoring: `AddServiceControlMonitoring` in `src/ServiceControl.Monitoring/HostApplicationBuilderExtensions.cs`.

However, the entrypoint/command types are internal and the extensions assume one role and one NServiceBus endpoint per service provider.

### Existing ServicePulse integration

Primary already references `Particular.ServicePulse.Core` and supports `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE`:

- setting and relative `/api/` override: `src/ServiceControl/Infrastructure/Settings/Settings.cs`;
- middleware mounting: `src/ServiceControl/Hosting/Commands/RunCommand.cs`;
- package reference: `src/ServiceControl/ServiceControl.csproj`.

A proposed `ServicePulse` role should therefore be a role/capability validation rule, not a fourth independently hosted application:

- `ServicePulse` should imply `Primary`, or be rejected unless `Primary` is selected;
- `All` should probably expand to `Primary,Audit,Monitoring,ServicePulse`;
- `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE=true` should remain compatible.

This contract needs an explicit product decision.

## Current image and packaging shape

There are three near-identical application Dockerfiles:

- `src/ServiceControl/Dockerfile` builds/copies `/deploy/Particular.ServiceControl` and starts `/app/ServiceControl`.
- `src/ServiceControl.Audit/Dockerfile` builds/copies `/deploy/Particular.ServiceControl.Audit` and starts `/app/ServiceControl.Audit`.
- `src/ServiceControl.Monitoring/Dockerfile` builds/copies `/deploy/Particular.ServiceControl.Monitoring` and starts `/app/ServiceControl.Monitoring`.

They differ in executable, exposed port, defaults, and health URL. The chiseled runtime image should not be assumed to contain a shell, so a .NET launcher is preferable to a shell supervisor.

The artifact model needs care:

- transports are build-only references and are copied as artifact side effects (`src/ProjectReferences.Transports.props`);
- Primary and Audit use separate persister artifact lists (`src/ProjectReferences.Persisters.Primary.props` and `.Audit.props`);
- each executable currently emits a separate deployment directory;
- installer packaging intentionally compares shared files and emits separate ZIPs (`src/ServiceControlInstaller.Packaging/ServiceControlInstaller.Packaging.csproj`).

The unified image should initially copy all three deployment directories without flattening them, for example `/app/primary`, `/app/audit`, and `/app/monitoring`. Flattening can overwrite same-named assemblies and bypass existing artifact/plugin behavior.

RavenDB should remain a separate image. `src/ServiceControl.RavenDB/Dockerfile` has a distinct base, storage lifecycle, and upgrade guard.

## Configuration source and naming behavior

`src/ServiceControl.Configuration/EnvironmentVariableSettingsReader.cs` first reads the namespaced environment key and then, for the three application namespaces only, falls back to the unprefixed key.

| Role | Logical namespace | Namespaced prefix | Example |
|---|---|---|---|
| Primary | `ServiceControl` | `SERVICECONTROL_` | `SERVICECONTROL_TRANSPORTTYPE` |
| Audit | `ServiceControl.Audit` | `SERVICECONTROL_AUDIT_` | `SERVICECONTROL_AUDIT_TRANSPORTTYPE` |
| Monitoring | `Monitoring` | `MONITORING_` | `MONITORING_TRANSPORTTYPE` |
| Shared queues | `ServiceBus` | `SERVICEBUS_` | `SERVICEBUS_ERRORQUEUE` |

For the first three namespaces, `TRANSPORTTYPE` is also accepted as a common fallback. Namespaced values win. The `ServiceBus` namespace does **not** allow this fallback.

This behavior is useful for shared transport/auth configuration, but unsafe for role-identity, persistence, and retention settings in a combined container.

## Configuration that should be shared across roles

“Shared” means the values must be operationally compatible. It does not necessarily mean they must be represented by one unprefixed variable.

| Configuration | Required relationship | Recommendation |
|---|---|---|
| `TransportType` | All selected messaging roles must use the same transport technology. | A shared `TRANSPORTTYPE` is appropriate; permit role-prefixed overrides for advanced cases but validate equality for a combined topology. |
| transport `ConnectionString` | Must point at the same logical broker/infrastructure for the roles to communicate. | A shared `CONNECTIONSTRING` is appropriate. Do not confuse this with persistence connection strings. |
| authentication authority/audience/validation | One ServicePulse token must be accepted by Primary, Audit, and Monitoring APIs. | Share unprefixed `AUTHENTICATION_*` values and validate consistency. `Authentication.ServicePulse.*` remains Primary-only. |
| role/RBAC claim names and authorization switch | Authorization must behave consistently on all APIs. | Share `RolesClaim`, `RoleBasedAuthorizationEnabled`, `SubjectIdClaim`, and `SubjectNameClaim`. |
| forwarded-header trust | Roles behind the same proxy must interpret host/scheme consistently. | Usually share `FORWARDEDHEADERS_*`. |
| CORS policy | ServicePulse must be allowed to call all exposed APIs. | Usually share `CORS_*`; validate the browser-visible ServicePulse origin. |
| TLS certificate/policy | Usually the same public deployment policy/certificate. | Sharing is sensible, but `Https.Port` is listener/redirect-specific and cannot blindly be one value when retaining three ports. |
| validation switch | `ValidateConfig` has the same semantics/default (`true`). | Safe to share. |
| shutdown timeout | Same semantics and current default (5 seconds). | Safe for separate processes; a single host needs one deliberate global value. |

### Values that must be coordinated but use different keys

| Relationship | Current keys | Risk |
|---|---|---|
| Primary and Monitoring error queue must match | `SERVICEBUS_ERRORQUEUE` vs `MONITORING_ERRORQUEUE` | Setting only unprefixed `ERRORQUEUE` configures Monitoring, not Primary. |
| Primary must know Audit API | `SERVICECONTROL_REMOTEINSTANCES` | In a combined container this can use loopback/internal ports, but browser-visible URLs have different requirements. |
| Audit must know Primary queue address | `SERVICECONTROL_AUDIT_SERVICECONTROLQUEUEADDRESS` | Must track Primary’s actual `InstanceName`; a rename can silently break custom-check routing. |
| Integrated ServicePulse must know Monitoring API | package variables `MONITORING_URL` / `MONITORING_URLS` | URL is consumed by a browser; container-local `localhost` may be wrong unless requests are same-host/proxied. |

## Configuration that must remain role-specific

### Identity and endpoint settings

- `InstanceName` must be unique for Primary, Audit, and Monitoring. A shared `INSTANCENAME` is a startup/routing blocker.
- `MaximumConcurrencyLevel` has the same shape but independently tunes each endpoint.
- ingestion/forwarding settings are role-specific.
- queue identities other than explicitly coordinated error-queue values are role-specific.

Always prefer:

- `SERVICECONTROL_INSTANCENAME`;
- `SERVICECONTROL_AUDIT_INSTANCENAME`;
- `MONITORING_INSTANCENAME`.

The launcher should reject a combined configuration in which selected roles resolve to duplicate endpoint names.

### Persistence settings

Primary and Audit may share a RavenDB server connection/certificate, but must not share storage identity:

| Raven setting | Sharing rule |
|---|---|
| server/cluster connection string | May be shared. |
| client certificate | May be shared if both databases grant access. |
| database name | Must differ (`primary` and `audit` defaults). |
| embedded `DbPath` | Must differ; one path cannot be independently owned twice. |
| database maintenance port | Must differ for separate embedded servers. |
| log path | Prefer separate paths/process-labelled output. |

Unprefixed `RAVENDB_*`, `DBPATH`, and `DATABASEMAINTENANCEPORT` are dangerous in combined roles because both Primary and Audit can consume them. Use role-prefixed values and add launcher validation.

The most important in-process blocker is embedded RavenDB: Primary and Audit independently use the process-global `EmbeddedServer.Instance`. Two embedded server lifecycles cannot simply be started by two role modules in one process. A true combined host must either:

- own one embedded server explicitly and open distinct `primary`/`audit` databases;
- require external RavenDB for combined roles; or
- retain separate role processes.

### Retention and similarly named settings

`AuditRetentionPeriod` is a concrete conflict:

- Primary treats it as optional/null.
- Audit code defaults it to 30 days.
- the Audit Dockerfile currently injects 7 days.

A shared unprefixed `AUDITRETENTIONPERIOD` changes both roles. Keep it namespaced.

Likewise, do not share merely because suffixes match:

- `PersistenceType` (providers may match, storage identities must not);
- `EnableFullTextSearchOnBodies`;
- `MaxBodySizeToStore`;
- `MaximumConcurrencyLevel`;
- `LogPath`;
- role retention, ingestion, forwarding, and retry timing values.

### Ports and hostnames

Container settings currently hard-code listeners in constructors:

- Primary `* : 33333` in `src/ServiceControl/Infrastructure/Settings/Settings.cs`;
- Audit `* : 44444` in `src/ServiceControl.Audit/Infrastructure/Settings/Settings.cs`;
- Monitoring `* : 33633` in `src/ServiceControl.Monitoring/Settings.cs`.

The normal `Port`/`Hostname` settings are ignored in containers. This is acceptable if the first combined implementation preserves all three ports, but blocks user-selectable internal ports and complicates one-public-port hosting.

### Logging conflict

`src/ServiceControl.Infrastructure/LoggingSettings.cs` mutates `LoggerUtil.ActiveLoggers` and `LoggerUtil.SeqAddress`, which are process-global static state. Constructing multiple role settings in one process means the last role initialized controls provider selection/Seq globally. The role host extensions also configure/clear logging providers independently.

For a true single process, either:

- logging provider and Seq settings become one global launcher/host configuration, with role-enriched log events; or
- logging setup is refactored to remove global mutable state.

Separate child processes avoid this conflict while allowing role-specific paths/providers.

## Why naive in-process embedding fails

### NServiceBus endpoint registration and DI

NServiceBus supports multiple endpoints in one Generic Host when each endpoint is registered with a unique identifier/key. Current registrations call `AddNServiceBusEndpoint(configuration)` without identifiers in all three host extensions.

The larger problem is endpoint-specific unkeyed registration. Each role registers types such as:

- `ITransportCustomization`/transport-specific services;
- `TransportSettings`;
- `Lazy<IMessageDispatcher>`;
- endpoint sessions/dispatchers consumed by hosted services and controllers.

In one root container, ordinary unkeyed resolution returns the last applicable registration. Merely adding endpoint identifiers is insufficient. A single-host design needs keyed services throughout or explicit role-specific façades such as `IPrimaryMessageSession`, `IAuditMessageSession`, and `IMonitoringMessageSession`.

### HTTP route collisions

Primary already contains composite Audit-facing APIs that call configured remote Audit instances. Audit exposes many of the same routes directly. Exact collisions include variants of:

- `/api/messages`;
- `/api/endpoints/{endpoint}/messages`;
- `/api/messages/{id}/body`;
- `/api/conversations/{conversationId}`;
- `/api/connection`;
- `/api/configuration`;
- `/api/instance-info`.

Loading both controller assemblies into one MVC application would create ambiguous matches and could bypass Primary’s existing aggregation behavior.

Monitoring uses root-relative routes such as `/`, `/connection`, and `/monitored-endpoints`; `/` conflicts with integrated ServicePulse’s Primary-root UI.

### Host-global web policy

Each role independently registers controllers, application parts, filters, model binders, CORS, authentication, authorization, HTTPS, and middleware. In one web host these accumulate into shared options. One role must become the policy owner, likely Primary, and embedded roles must contribute explicitly designed APIs rather than importing their current web stacks unchanged.

### Lifecycle and failure coupling

Audit relies on registration order so NServiceBus starts before audit ingestion. Primary and Audit persistence also use hosted-service lifecycles. In one host:

- one role’s startup failure prevents all roles from starting;
- shutdown is one unit;
- there is one effective `HostOptions.ShutdownTimeout`;
- the desired response to one endpoint’s critical error must be defined (stop all, degrade one, or restart one).

A launcher also needs an explicit policy, but process isolation makes behavior and cleanup clearer.

## Architecture options

### Option A — one image, one selected role per container

One launcher selects exactly one existing role.

**Pros:** directly removes image variants; preserves behavior; lowest implementation risk; existing ports, DI, routes, persistence, and command modes remain intact.

**Cons:** does not reduce container count for small installations.

This should be the first deliverable regardless of later composition.

### Option B — one entrypoint supervising isolated role processes (recommended first combined mode)

The image contains all three artifacts. A small .NET PID-1 launcher starts selected executables and preserves the current isolation boundaries.

**Pros:** enables `Primary,Audit,Monitoring`/`All`; avoids DI, route, static logging, and embedded Raven process-global collisions; reuses existing commands and setup behavior.

**Cons:** multi-process container supervision and aggregate health are required; memory/process footprint remains near the current total; one container becomes a shared scaling/failure boundary.

The launcher must forward SIGTERM/SIGINT, stop all children when one fails unexpectedly, preserve role-labelled output, wait within the platform shutdown budget, and return a useful non-zero exit code.

### Option C — one process with independent child hosts/service providers

A launcher creates a separate `WebApplication` per role in one process, preserving ports and route/DI isolation.

**Pros:** fewer processes while retaining service-provider isolation.

**Cons:** process-global logging and embedded Raven remain blockers; host/signal ownership is more complex; repeated host-global/static setup was not designed for this.

This is only attractive with external RavenDB and after logging/bootstrap refactoring.

### Option D — Primary web host with embedded Audit/Monitoring workers and façades

Primary owns the only public web pipeline. Audit and Monitoring run as worker modules/endpoints; their existing controllers are not imported unchanged. Primary’s existing composite Audit APIs access local Audit persistence when embedded, and Monitoring gets deliberately designed façade routes.

**Pros:** closest to a true integrated product and to the desired “like integrated ServicePulse” user experience; can eventually expose one public origin.

**Cons:** large refactor of keyed endpoint services, persistence ownership, APIs, URL compatibility, and lifecycle. It is not analogous to mounting ServicePulse middleware.

This is a viable long-term target, not the safest first implementation of #5028.

## Recommended implementation outline

### 1. Define the role contract

Decide and test:

- canonical environment variable spelling (`SERVICE_CONTROL_ROLE` as proposed by the issue, or a convention-aligned alternative);
- case/whitespace/duplicate handling;
- omitted value (recommend `Primary` for the canonical Primary image’s compatibility);
- valid values: `Primary`, `Audit`, `Monitoring`, `ServicePulse`, `All`;
- whether `ServicePulse` implies Primary;
- whether `All` includes ServicePulse;
- unknown-role diagnostics;
- whether maintenance/import arguments are legal with multiple selected roles (recommend rejecting ambiguous multi-role non-run commands initially).

An environment-based role survives `IntegratedSetup.Run` child execution automatically. If a role argument is supported, ensure it survives the `--setup-and-run` re-exec path.

### 2. Add a container-focused launcher

Prefer a small .NET executable as the sole OCI `ENTRYPOINT`. Keep Windows installer executables and ZIPs unchanged.

For each selected role, launch the existing artifact/entrypoint with the original arguments. Keep role application directories isolated. Explicitly model child startup, failure, cancellation, signal propagation, output attribution, and exit codes.

Avoid changing `Assembly.GetExecutingAssembly()` semantics in existing programs unless their app-config loading is deliberately refactored; simply moving all current `Program.cs` bodies into a new executing assembly could change legacy configuration behavior.

### 3. Build one application image

- Build all three applications and the launcher/health helper.
- Copy all three existing deployment artifacts into separate directories.
- Include both Primary and Audit persisters and all transports via the existing artifact targets.
- Expose 33333, 44444, and 33633 initially.
- Keep RavenDB image/build independent.
- Apply role defaults with namespaced variables or launcher defaults; do not inject Audit’s unprefixed retention default globally.

### 4. Add role-aware health

Current Docker health checks assume one fixed endpoint:

- Primary: `http://localhost:33333/api/configuration`;
- Audit: `http://localhost:44444/api/configuration`;
- Monitoring: `http://localhost:33633/connection`.

The unified helper must inspect selected roles and require each selected child and role-specific HTTP endpoint to be healthy. Unselected roles must be ignored. Consider separate startup/readiness and liveness semantics; at minimum diagnostics should identify the failed role.

### 5. Update release/deployment assets

Affected areas include:

- `.github/workflows/build-containers.yml` (three-image matrix);
- `.github/workflows/push-container-images.yml` (three repositories/descriptions);
- `.github/workflows/clean-ghcr.yml`;
- role container READMEs and `docs/deployment.md`;
- `src/container-integration-test/servicecontrol.yml` and transport overlays;
- synchronized external compose examples listed at the top of that file.

Old image names cannot be transparent OCI aliases if each alias requires a different default role: an image alias cannot inject role-specific environment metadata. Compatibility requires temporary wrapper images, role-specific entrypoint defaults, or a documented migration to one canonical repository.

### 6. Test in increments

1. Use the unified image three times, one role per container, across the existing transport matrix.
2. Add one combined container selecting all roles.
3. Verify role-specific endpoints plus actual behavior, not only container health.
4. Verify Primary-to-Audit aggregation and Audit-to-Primary queue routing.
5. Verify Monitoring uses the same error queue as Primary.
6. Verify integrated ServicePulse UI and browser-visible Monitoring URL.
7. Test shared unprefixed transport/auth values and namespaced identity/persistence overrides.
8. Test all role parsing, setup/re-exec, process failure, signals, and aggregate health.
9. Test upgrades from each old image to the equivalent role in the unified image without data changes.

## Validation recommended at launcher startup

For combined roles, fail fast with actionable errors when:

- selected role endpoint names collide;
- selected roles resolve to different transport types;
- required transport connection settings are absent/incompatible;
- Primary and Monitoring error queues differ;
- Primary and Audit resolve to the same Raven database, embedded path, or maintenance port;
- Audit’s Primary queue address conflicts with Primary’s endpoint name;
- authentication authority/audience/RBAC settings differ across exposed APIs;
- `ServicePulse` is selected without its required Primary relationship;
- a multi-role command is ambiguous;
- selected roles attempt to bind the same port;
- combined in-process mode is requested with unsupported embedded Raven/logging configuration.

## Final recommendation

Implement #5028 first as **one canonical image plus one role-aware .NET entrypoint**, preserving the three existing role applications internally. Support one role per container first, then composable roles through supervised isolated processes. This satisfies image consolidation and offers the requested small-shop topology without forcing an immediate rewrite of ServiceControl’s hosting model.

Do not initially host Audit and Monitoring in Primary by calling their existing host extensions beside `AddServiceControl`. To make that model safe requires a dedicated modularization effort: keyed NServiceBus endpoints and endpoint-specific services, one web-policy owner, removal/replacement of colliding controllers, explicit local Audit/Monitoring façades, one persistence ownership model, one logging model, and defined cross-role failure semantics.

If future profiling proves that the multi-process combined container does not deliver enough footprint reduction, evolve toward the Primary-owned façade/worker model (Option D), using Primary’s existing composite Audit API as the seam rather than importing Audit’s duplicate HTTP API.