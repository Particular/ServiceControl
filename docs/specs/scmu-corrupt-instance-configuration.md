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
the stopped indicator is shown, and the error banner explains what failed.

- **Example:** The one where the status reads CONFIGURATION ERROR and neither the
  running nor the stopped indicator is shown.
- **Example:** The one where the error banner explains that the configuration failed
  to load.
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
- **Counter-example:** The one where a refresh tries to apply data from a
  differently-named instance and is rejected.

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

## Gaps not covered by the current implementation

1. **Dead list-level banner.** `ListInstancesViewModel.HasConfigurationErrors`,
   `ConfigurationErrorMessage`, and `InstancesWithConfigErrors` are bound to nothing —
   no XAML references them (the only banner shipped is the per-instance one in
   `InstanceDetailsView`). The `BoolToVisibilityConverter` resource added to
   `ListInstancesView.xaml` is likewise unused. Either wire a list-level banner or
   delete the dead code.
2. **List view model is untestable.** `ListInstancesViewModel` calls the static
   `InstanceFinder.AllInstances()` in its constructor, so the banner/refresh logic
   cannot be driven by tests without enumerating real Windows services. Needs a seam
   (same pattern as `GetInstalledErrorInstanceNames` in PR #5637).
3. **Unused details-VM property.** `InstanceDetailsViewModel.ConfigurationFilePath`
   is referenced by nothing; the banner text also never tells the operator *which
   file* to fix, although the path is captured.
4. **`UpdateServiceInstance` silently ignores type changes.** If the fresh instance
   has the same name but a different type, the update is dropped without error.
