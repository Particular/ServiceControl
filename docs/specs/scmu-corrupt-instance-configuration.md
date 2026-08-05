# Feature: SCMU resilience to corrupt instance configuration files

> [!WARNING]
> **This document is a working artifact for the development of this fix and is
> meant to be DELETED before merging the PR.** Do not reference it from code or docs
> that will outlive the PR.

**As an operator managing instances through ServiceControl Management Utility (SCMU),
I want SCMU to start and show every installed instance even when one of them has a
corrupt configuration file, so that I can see which instance is broken and fix it
instead of being locked out of the tool entirely.**

> Bug: [#2759 — SCMU: Fails to start when one of the instances has a corrupt configuration file](https://github.com/Particular/ServiceControl/issues/2759).
> When any instance's `.exe.config` contains invalid XML, loading it throws
> `ConfigurationErrorsException` during instance enumeration and SCMU crashes at
> startup. The only workaround is fixing the file blind or falling back to the
> PowerShell module.

## Vocabulary

- **SCMU** = ServiceControl Management Utility (`ServiceControl.Config`), the Windows
  desktop tool used to install and manage instances.
- **Instance** = an installed ServiceControl Windows service; three types exist:
  **error instance** ("ServiceControl"), **audit instance** ("ServiceControl Audit"),
  and **monitoring instance** ("ServiceControl Monitoring").
- **Configuration file** = the instance's `<exe name>.exe.config` in its install path
  (e.g. `ServiceControl.exe.config`), read by SCMU to display and edit settings.
- **Corrupt configuration** = a configuration file that cannot be loaded — typically
  invalid XML; loading it throws instead of returning settings.
- **Configuration error state** = how a corrupt instance is presented: loaded and
  listed, but flagged with a `CONFIGURATION ERROR` status, its settings unavailable
  and config-dependent actions blocked.
- **DEPLOYED INSTANCES screen** = SCMU's main screen listing all installed instances.
- **Per-instance banner** = the error banner on an instance's card; **summary
  banner** = the banner above the instance list naming all corrupt instances.
- **Refresh** = the DEPLOYED INSTANCES action that re-reads every instance's
  configuration from disk and updates each instance card in place.

## Rules and Examples

### Rule 1: Must load every installed instance even when its configuration file is corrupt

Enumeration must never let one broken instance take down the whole list. A corrupt
instance still appears, carrying a configuration error instead of its settings; its
display name falls back to the Windows service name (the config it would normally
be read from is unreadable).

- **Example:** The one where the error instance's config XML is corrupt and the
  instance still loads, flagged with a configuration error.
- **Example:** The one where the audit instance's config XML is corrupt and the
  instance still loads, flagged with a configuration error.
- **Example:** The one where the monitoring instance's config XML is corrupt and the
  instance still loads, flagged with a configuration error.
- **Counter-example:** The one where the configuration is valid and no configuration
  error is flagged.

---

### Rule 2: Must show the configuration error in place of the service status

The affected instance must be visibly broken in the UI: the status line reads
`CONFIGURATION ERROR` instead of the Windows service status, neither the running nor
the stopped indicator is shown, and the error banner explains what failed and names
the file the operator has to fix.

- **Example:** The one where the status reads CONFIGURATION ERROR and neither the
  running nor the stopped indicator is shown.
- **Example:** The one where the error banner explains that the configuration failed
  to load and names the config file.
- **Counter-example:** The one where the configuration is valid and the Windows
  service status is shown as usual.

---

### Rule 3: Must block actions that require a valid configuration while the error persists

Starting, stopping, editing, or viewing advanced options of an instance whose
configuration cannot be read would operate on unknown state. Everything derived from
configuration (transport, persister) is also unavailable.

- **Example:** The one where Start and Stop are not allowed.
- **Example:** The one where Edit and Advanced Options are hidden.
- **Example:** The one where no transport or persister is reported.
- **Counter-example:** The one where the configuration is valid and the instance can
  be edited and started as usual.

---

### Rule 4: Should recover the instance once the configuration file is fixed and the list is refreshed

The operator's fix loop is: see the error → repair the file on disk → refresh SCMU.
Refresh must re-read the configuration from disk and clear the error state without
restarting SCMU. Refresh updates each instance in place — it never swaps state
between instances.

- **Example:** The one where the config file is fixed on disk, refresh runs, and the
  instance returns to normal (service status shown, edit allowed).
- **Example:** The one where the config file becomes corrupt after loading and the
  next refresh flags the error.
- **Example:** The one where the fix is picked up through the DEPLOYED INSTANCES
  refresh flow (the same path the UI triggers), not just by updating a single
  instance directly.
- **Counter-example:** The one where a refresh tries to apply data from a
  differently-named instance and is rejected.
- **Counter-example:** The one where a refresh tries to apply data from an instance
  of a different type (same name) and is rejected.

---

### Rule 5: Must summarize configuration errors above the instance list

The per-instance banner can be far below the fold in a long list. A summary banner at
the top of the DEPLOYED INSTANCES screen names the corrupt instance(s) so the operator
sees at a glance that something needs fixing, and disappears when everything is
healthy.

- **Example:** The one where a single instance is corrupt and the banner names it.
- **Example:** The one where multiple instances are corrupt and the banner lists all
  of them.
- **Counter-example:** The one where all configurations are valid and no banner is
  shown.

## Resolved decisions (for implementation)

- **Acceptance tier:** `InstanceDetailsViewModel` observed over real
  `ServiceControlInstance` / `ServiceControlAuditInstance` objects loaded from real
  (corrupt or valid) config files in a temp folder, with the Windows service
  substituted through the existing `IWindowsServiceController` seam. A full SCMU
  end-to-end test (real Windows services) is not automatable in this repository's
  test suites.
- **Healthy instances are real, not simulated:** a minimal valid config with
  `ServiceControl/TransportType = LearningTransport` fully loads through
  `Reload()`, so counter-examples exercise the genuine success path.
- **Error detection point:** `AppConfigWrapper` captures the open failure;
  instance constructors catch any `Reload()` failure, set
  `ConfigurationLoadError`, fall back to the service name, and record the error on
  the `ReportCard`. Both paths are covered by Rule 1 regardless of whether
  `ConfigurationManager` throws eagerly (at open) or lazily (at first read).
- **All three instance types are protected the same way** (Rule 1):
  `MonitoringInstance` takes the `IWindowsServiceController` seam like the other two
  and creates its `AppConfig` inside the same try/catch, so a corrupt monitoring
  config is caught and testable identically.
- **No silent skip** (Rule 1): the enumeration in `InstanceFinder` does not wrap
  instance construction in a try/catch that omits failing instances (and does not
  write ad-hoc logs to `%TEMP%`). The constructors themselves never throw for config
  errors — they load the instance in the error state instead. An instance that fails
  to load must be visible, not missing.
- **Error message names the file** (Rule 2): `ConfigurationLoadError` is formatted as
  `Failed to load configuration file '<path>': <reason>`, so both banners tell the
  operator exactly which file to fix.
- **List-level banner** (Rule 5): `ListInstancesView` binds a summary banner to
  `ListInstancesViewModel.HasConfigurationErrors` / `ConfigurationErrorMessage`.
  The view model takes a `getAllInstances` seam (internal constructor overload,
  defaulting to `InstanceFinder.AllInstances`) so the banner logic is testable
  without enumerating real Windows services.
- **Refresh never swaps state between instances** (Rule 4): `UpdateServiceInstance`
  rejects an update whose name *or* type differs from the instance the view model
  wraps, instead of silently ignoring it.
