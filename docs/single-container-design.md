# Single ServiceControl container design

## Status

This document records the contracts for Particular/ServiceControl#5028 before implementation. These contracts are intended to remain stable and to be covered by automated tests.

The implementation will ship one canonical Linux application image containing the existing Primary, Audit, and Monitoring applications. A .NET launcher will run as PID 1 and select one or more applications as isolated child processes. It will not combine their command parsers, dependency injection containers, NServiceBus endpoints, HTTP hosts, or persistence lifecycles.

A true single-process, single-web-host architecture is not part of this design.

## Role-selection contract

The launcher reads `SERVICE_CONTROL_ROLE`.

- When the variable is absent, the selected role is `Primary`.
- An explicitly empty or whitespace-only value is invalid.
- Values are case-insensitive.
- Comma-separated values are accepted and surrounding whitespace is trimmed.
- Empty elements are invalid. For example, `Primary,,Audit` is rejected.
- Duplicate values are removed.
- Unknown values are rejected with a message containing the complete allowed-value list.
- Diagnostics and health output use canonical role names.

The allowed values are:

- `Primary`
- `Audit`
- `Monitoring`
- `ServicePulse`
- `All`

`Primary`, `Audit`, and `Monitoring` are process roles. `ServicePulse` is a capability, not a fourth process role.

Role expansion follows these rules:

- `ServicePulse` implies the `Primary` process role and enables the `ServicePulse` capability.
- `All` expands to the `Primary`, `Audit`, and `Monitoring` process roles plus the `ServicePulse` capability.
- Process roles start in canonical order: Primary, Audit, Monitoring.
- Start order is deterministic but does not represent a readiness dependency.

## Role descriptors

The launcher owns immutable descriptors for the process roles:

| Role | Executable | Working directory | Health endpoint | Port |
|---|---|---|---|---:|
| Primary | `/app/primary/ServiceControl` | `/app/primary` | `http://localhost:33333/api/configuration` | 33333 |
| Audit | `/app/audit/ServiceControl.Audit` | `/app/audit` | `http://localhost:44444/api/configuration` | 44444 |
| Monitoring | `/app/monitoring/ServiceControl.Monitoring` | `/app/monitoring` | `http://localhost:33633/connection` | 33633 |

The application root will be injectable for tests. Production uses `/app`.

## Launcher command contract

The launcher supports two modes:

- default or `run`: validate configuration and supervise selected process roles;
- `health`: probe all selected process-role endpoints and report aggregate health.

Launcher-owned modes and options are consumed by the launcher. Application arguments are otherwise passed to children unchanged; the launcher does not parse or recreate the existing application command contracts.

For one selected process role, all application arguments are passed through unchanged. This preserves setup, maintenance, import, and help behavior.

For multiple selected process roles, only these application argument sets are supported initially:

- no arguments, for a normal run;
- exactly `--setup-and-run`.

Maintenance, import, help, setup-only, and other commands are rejected for multiple process roles with an actionable message instructing the user to select and operate one process role at a time.

Invalid role selections and unsupported command combinations return a stable usage/configuration failure distinct from a child-process failure.

`SERVICE_CONTROL_ROLE` remains available in each child's inherited environment, including processes re-executed by `IntegratedSetup.Run`.

At startup, the launcher reports selected process roles, capabilities, executable paths, and ports. It never reports connection strings, certificates, tokens, or other secret configuration values.

## Integrated ServicePulse contract

Selecting the `ServicePulse` capability does not start another process.

Before Primary starts:

- if `SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE` is unset, the launcher sets it to `true` in the Primary child's environment;
- if it is explicitly `false`, startup fails with an actionable conflict message;
- an explicitly enabled value remains enabled.

Package-owned `MONITORING_URL` and `MONITORING_URLS` values are preserved. They are browser-visible URLs and generally must not use container-local `localhost` when ServicePulse is accessed externally.

## Process and failure contract

Every selected executable must exist before any child is started. Children run from their role-specific working directories without a shell and inherit the launcher environment except for documented capability-specific overrides.

The launcher observes every child from startup.

- Any unexpected child exit initiates shutdown of all remaining children.
- A non-zero child exit is propagated when its exit code is usable; otherwise the launcher returns a stable launcher failure.
- A zero exit is still an unexpected failure while sibling roles are expected to continue running. A multi-role container never remains running in a partially healthy state.
- Simultaneous exits are resolved deterministically for diagnostics and exit-code selection.

Child stdout and stderr are inherited unchanged so structured logs remain byte-for-byte valid and the two streams remain separate. The launcher emits only its own role-labelled lifecycle events; it does not prefix or rewrite child output.

## Shutdown contract

The launcher handles SIGTERM and SIGINT and also supports `Console.CancelKeyPress` during local execution.

On shutdown it:

1. forwards the matching graceful termination signal to every direct child;
2. waits for all children for the launcher shutdown grace period;
3. kills the entire process tree of each remaining child after the grace period;
4. waits for output handling to complete and disposes process and signal registrations before exiting.

The grace period is configured by `SERVICECONTROL_LAUNCHER_SHUTDOWN_TIMEOUT` and defaults to `20s`. An invalid or non-positive value is a configuration error. The timeout must leave the role applications time to shut down within the hosting platform's overall stop timeout.

Native signal delivery is implemented directly and does not depend on a shell. Process-tree escalation prevents an `IntegratedSetup.Run` descendant from being orphaned.

## Health contract

`health` parses and expands `SERVICE_CONTROL_ROLE` using the same implementation as run mode. It probes all selected process-role endpoints concurrently with a short per-request timeout.

A role is healthy only when its endpoint returns:

- an HTTP success status;
- an `application/json` content type;
- a non-empty response body.

Unselected roles are ignored. The `ServicePulse` capability is covered by the Primary endpoint and does not add a fourth health probe.

Health output contains one result per selected process role and a final aggregate result. The command returns success only when every selected role passes. Errors name failed roles but do not include response bodies or configuration values.

Child liveness is enforced by supervision: when a child exits, PID 1 exits and the container stops.

## Compatibility and publishing contract

The first deployment increment uses the canonical image once per role as a drop-in replacement for the existing images. Combined-role operation follows after that compatibility path is proven.

The canonical image is `particular/servicecontrol`. Migration is:

| Old image | New image | Required role |
|---|---|---|
| `particular/servicecontrol` | `particular/servicecontrol` | omitted or `Primary` |
| `particular/servicecontrol-audit` | `particular/servicecontrol` | `Audit` |
| `particular/servicecontrol-monitoring` | `particular/servicecontrol` | `Monitoring` |

The old Audit and Monitoring repositories will be published as thin compatibility wrapper images for one major-version transition. Each wrapper supplies its role-specific default. A plain OCI alias is not used because an alias cannot inject a different default role.

The RavenDB image remains separate as `particular/servicecontrol-ravendb`.

The unified application image retains isolated deployment directories and exposes ports 33333, 44444, and 33633. The .NET launcher is its only OCI entrypoint. Installer ZIPs, executable entrypoints outside the container image, and Windows installer packaging are unchanged.

Existing volumes, databases, and queues remain unchanged when equivalent role-specific settings are preserved during migration.

## Configuration ownership

Each child application retains ownership of its existing validation. The launcher adds only cross-role validation that an individual process cannot perform safely.

Shared transport and authentication values remain distinct from role-specific identity, persistence, and retention values. Combined-role operation must fail before starting any child when selected roles have unsafe identity, transport, queue, persistence, authentication, capability, command, or listener-port conflicts.

Validation diagnostics name roles and setting keys and explain the required relationship. Connection strings, certificates, tokens, and other secret values are always redacted.

## Deferred work

This design does not:

- combine the three applications in one dependency injection container;
- expose all roles through one HTTP port;
- flatten application artifacts into one directory;
- embed RavenDB in the application image;
- reduce a combined-role container to one application process.

A single-process host requires a separate architecture decision after measuring image size, memory use, startup time, and operational complexity of the supervised design.
