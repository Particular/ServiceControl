# Platform Health API

ServiceControl exposes `GET /api/platform-health` for the ServiceControl-owned data on ServicePulse's Platform Health page. The API root advertises its URL in `platform_health`. The response uses the existing snake_case JSON convention and omits unknown nullable fields.

The public motivation is [ServiceControl #5860](https://github.com/Particular/ServiceControl/issues/5860). The consumer data requirements were checked against [ServicePulse's Platform Health store](https://github.com/Particular/ServicePulse/blob/e2688743d23fe5a4b835d4cff128ad12da87d34c/src/Frontend/src/stores/PlatformHealthStore.ts) and [platform model](https://github.com/Particular/ServicePulse/blob/e2688743d23fe5a4b835d4cff128ad12da87d34c/src/Frontend/src/resources/PlatformModel.ts).

## Response

The existing `status`, `severity`, and `alerts` fields remain, with additive `instances` and `license` sections.

### Instances

`instances` contains the primary followed by every distinct configured remote, even when no check has reported or a remote cannot be reached. Remotes are ordered by stable ID, not by the order that their requests complete.

| Field | Meaning and source |
| --- | --- |
| `id` | Existing URL-derived ServiceControl instance ID; independent of display name and row position |
| `name` | Configured instance name; a never-observed remote falls back to its URI hostname |
| `kind`, `role` | `error` / `primary-error`, `error` / `remote-error`, `audit` / `remote-audit`, or `unknown` / `remote-unknown` |
| `api_url` | Request-facing primary URL, honoring forwarded scheme, host and prefix; configured remote URL with its virtual directory preserved |
| `version` | Installed local version or remote `X-Particular-Version`; absent when unknown, never replaced with the primary's version |
| `host_id` | Actual reporting host identity from the local NServiceBus host or remote configuration; absent on older remotes |
| `health` | `healthy` for reachable instances without an associated failure, `degraded` for reachable instances with failures, `unavailable` for failed probes |
| `observed_at` | UTC timestamp for the current refresh, from the injected clock |
| `metadata_observed_at` | Timestamp of the last successful metadata observation; differs from `observed_at` during an outage |
| `health_signals_status` | `reported`, `unreported`, `disabled`, or `ambiguous`; not a guarantee that every possible check has run |
| `last_reported_at` | Latest associated check timestamp, including successful reports; distinct from HTTP observation time |
| `issues` | Associated failed internal checks, with the same fields as root alerts |
| `transport_type`, `error_queue`, `error_log_queue`, `forward_error_messages` | Available transport configuration; a known `false` forwarding setting is preserved |
| `audit_queue`, `audit_log_queue`, `forward_audit_messages` | Available audit transport configuration |
| `error_retention_period`, `audit_retention_period` | Available retention durations in the existing TimeSpan JSON format, for example `14.00:00:00` |

Primary and audit `/api/configuration` (also `/api/instance-info`) include `instance_type` and `host.host_id`. Primary configuration additionally reports `health_checks_enabled`. Older remotes without `instance_type` are identified only when their retention configuration establishes the type. A never-observed, unreachable remote is explicitly unknown, not assumed to be an audit instance.

Remote probes use the registered named HTTP clients and their query timeout. Non-success status codes, empty or malformed configuration, and connection failures do not produce healthy rows. An outage retains the last successful metadata in memory, clearly dated by `metadata_observed_at`. Other rows and the license section still return. Caller cancellation propagates instead of returning partial success. No recursive platform-health requests are made to other primaries.

### Issues and summary

Each failed check has `id`, `check_id`, `category`, `message`, `reported_at`, `instance_name`, `host`, and `host_id`. An associated issue also has `instance_id`.

Association uses case-insensitive instance name plus reporting host ID. A legacy remote without a host ID can use a name match only when there is one matching inventory row and one reporting host with that name. Ambiguous or unmatched reports remain in root `alerts` without `instance_id`; they are never assigned to several rows. Consumers should retain a place to display those unassigned alerts.

The legacy summary describes captured checks, not the whole browser-visible platform: `status` is `unknown` before any internal report, `healthy` when none are failing, and `unhealthy` when at least one is failing. Its corresponding `severity` values are `unknown`, `none`, and `error`. ServicePulse should use per-instance health for page severity and combine it with its independently observed monitoring state. The legacy summary does not account for monitoring, browser connectivity, license expiry, or available upgrades.

Check state is process-local. Reports older than a check's latest `reported_at` are ignored; a newer successful report clears that failure. Reports do not expire: different checks have different schedules, including one-shot checks. After restart, check observations and last-known remote metadata are initially empty. `healthy` therefore means reachable without a known associated failure, not proof of complete or fresh check coverage. An unreachable process cannot report its own browser-facing unavailability in a successful response.

### License

`license.availability` is `available` after a successful refresh and `unavailable` when license details cannot be refreshed. An unavailable license never claims to be valid and does not suppress instance health.

The available summary includes `status`, `license_status`, `license_type`, `trial_license`, optional `expiration_date` and `upgrade_protection_expiration`, and `license_extension_url`. It preserves the existing license status values for subscription, trial and upgrade-protection gates. Renewal URLs share the `/api/license` mapping with `clientName=servicepulse`, including MassTransit evaluation/subscription links. `has_mass_transit_connector` reports connector presence. Customer registration, licensed products and endpoint-license metadata are not included.

## ServicePulse integration

The endpoint supplies primary/remote inventory, installed versions, configuration, issues, and the license summary. Updating this endpoint does not update the ServicePulse consumer automatically; the consumer must map `instances` and `license` into its stores and support unknown instance types and unassigned alerts.

ServicePulse continues to own:

- Its running frontend version and ServicePulse row.
- The browser-selected monitoring URL, monitoring requests, and monitoring row.
- Browser-to-primary connectivity failures, including when this endpoint cannot be reached.
- Release-feed requests, latest-version comparison, release links, upgrade badges, and outdated-only navigation state. Installed version and license validity are not a guarantee that an upgrade path is supported.
- The customer-check fetch for the support export. Export combines this response, browser-owned rows, and the existing custom-check results. Customer checks never affect platform health.

Keep the legacy consumer fallback for supported ServiceControl versions without the advertised capability. Do not interpret `401`, `403`, a timeout, or a failed response as an absent capability. The existing custom-check API, classification, notifications, and integration events remain unchanged. Audit health still arrives through the current custom-check reporting transport; this increment does not remove that dependency or introduce replacement events.

## Access

The endpoint retains `error:customchecks:view`, granted by the existing reader, writer and admin roles. No permission or authentication behavior is changed. With authentication disabled it is anonymous. With authentication and RBAC enabled, anonymous callers receive `401` and authenticated callers without a read role receive `403`.

Known shared-policy limitation: authentication enabled with RBAC disabled currently resolves named permissions to allow-all, so the expanded response, including the license summary, can be accessed anonymously. Fixing that policy is separate work. Container liveness and readiness remain separate at `/health` and `/health/ready`.

## Verification

For manual requests, use [PlatformHealth.http](../src/ServiceControl/PlatformHealth.http). Its authenticated request reads an existing bearer token from `SERVICECONTROL_ACCESS_TOKEN`; do not store credentials in the request file.

`PlatformHealthStateTests` and `PlatformHealthApiTests` cover snapshot ordering, delayed reports, serialization, source projection, identity ambiguity, offline metadata, partial failures, license mapping and cancellation. Remote-client tests cover HTTP status, malformed responses and prefixed URLs. Shared acceptance scenarios exercise the real root/configuration/health responses and preserve custom-check behavior. The multi-instance `When_inspecting_platform_health` scenario exercises real audit check delivery, issue ownership, recovery and unavailable inventory. OIDC acceptance scenarios cover the existing read-role policy.