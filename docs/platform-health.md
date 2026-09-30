# Platform Health API

ServiceControl exposes `GET /api/platform-health` for ServicePulse to read ServiceControl's internal health signals without treating them as customer custom checks. The API root response advertises the endpoint through `PlatformHealth`.

The response contains an overall `status` and `severity`, plus an `alerts` array for currently failing internal checks. Each alert includes the check id, category, failure message, report time, instance name, host, and host id. Status is `unknown` before any internal check has reported, `healthy` when internal checks have reported and none are failing, and `unhealthy` when one or more are failing. Severity is `unknown`, `none`, or `error` for those respective states.

Health state is held in memory by the ServiceControl process. Each new report replaces the current state for that check, so a successful report clears its alert. The endpoint does not currently age out checks that stop reporting; a previously reported failure remains until that check reports again or the process restarts. ServicePulse should retain its custom-check fallback for older ServiceControl versions during rollout.

This initial response does not include version or license/upgrade information. It also does not replace `/api/customchecks` or the existing custom-check integration events. Internally, the transitional implementation still recognizes shipped checks through `InternalCustomCheckClassification`; removing that classification requires a coordinated contract and migration for audit-originated health reports.

The endpoint uses the existing `error:customchecks:view` authorization policy. Container liveness and readiness remain separate at `/health` and `/health/ready`.