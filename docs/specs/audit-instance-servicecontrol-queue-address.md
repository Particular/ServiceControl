# Feature: Audit Instance ServiceControl Queue Address

**As an operator adding instances through ServiceControl Management Utility (SCMU), I want every audit instance to be connected to a ServiceControl (error) instance so that messages the audit instance sends to the error instance always have a valid destination.**

> Bug: [#4753 — SCMU does not set ServiceControlQueueAddress when only adding audit instances](https://github.com/Particular/ServiceControl/issues/4753).
> When the `ServiceControl.Audit/ServiceControlQueueAddress` setting is missing from
> `ServiceControl.Audit.exe.config`, the audit instance fails at runtime with
> ["no destination specified for message"](https://docs.particular.net/servicecontrol/troubleshooting#no-destination-specified-for-message).

## Rules and Examples

### Rule 1: Must address the audit instance to the error instance installed in the same session

When the user installs an error instance and an audit instance together, the audit
instance's queue address is the name of the error instance being installed — never an
already-installed one.

- **Example:** The one where both instances are installed together and the audit
  instance's queue address is the new error instance's name.
- **Counter-example:** The one where other error instances already exist on the
  machine, yet no choice is offered — the error instance being installed always wins.

---

### Rule 2: Should auto-detect the existing error instance when adding an audit instance alone

- **Example:** The one where exactly one error instance exists on the machine and its
  name is used as the queue address without any user input (no dropdown shown).

---

### Rule 3: Must require an explicit choice when multiple existing error instances are found

Auto-detection cannot guess between several error instances — picking one silently
risks routing messages to the wrong instance. The choice dropdown is shown **only** in
this case.

- **Example:** The one where two error instances exist and the dropdown offers both.
- **Example:** The one where Save is blocked until the user picks one of the detected
  instances, and unblocked once a choice is made.

---

### Rule 4: Must block installation when no error instance exists to connect to

An audit instance without a reachable error instance is misconfigured by definition;
SCMU must not produce it.

- **Example:** The one where no error instance exists, the user adds an audit instance
  alone, and a validation error prevents the installation from proceeding.
- **Counter-example:** The one where only an error instance is being installed — the
  queue address does not apply and no validation error is raised.

## Resolved decisions (for implementation)

- **Auto-detect source:** installed Windows error instances, discovered via
  `InstanceFinder.ServiceControlInstances()`; exposed on the view model through a
  `GetInstalledErrorInstanceNames` function seam (mirrors the existing
  `GetWindowsServiceNames` pattern) so tests can substitute it.
- **Multiple instances found:** user must choose from a dropdown that is visible only
  when adding an audit instance alone **and** more than one error instance is detected.
- **No instance found:** Save is blocked by a validation error; deploying with
  PowerShell remains the path for advanced scenarios.
- **Acceptance tier:** view model + validator observed through `INotifyDataErrorInfo`
  — the same mechanism the UI uses to block Save. A full SCMU end-to-end test (install
  a Windows service, inspect the written config file) is not automatable in this
  repository's test suites.
- **Out of scope:** registering the new audit instance as a remote of the existing
  error instance (`AddRemoteInstance` is only called when both instances are installed
  together) — candidate for a follow-up issue.
