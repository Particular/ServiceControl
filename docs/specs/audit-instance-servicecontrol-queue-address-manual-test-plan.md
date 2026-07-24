# Manual Test Plan: Audit Instance ServiceControl Queue Address (SCMU)

> [!WARNING]
> **This document is a working artifact for the manual validation of this feature and
> is meant to be DELETED before merging the PR.** Record the test results here (or in
> a copy) while the PR is open.

**Spec:** [audit-instance-servicecontrol-queue-address.md](audit-instance-servicecontrol-queue-address.md)
**Bug:** [#4753 — SCMU does not set ServiceControlQueueAddress when only adding audit instances](https://github.com/Particular/ServiceControl/issues/4753)

This plan validates, end-to-end on a real machine, the rules implemented in the SCMU
"Add ServiceControl" screen. The automated acceptance tests cover the view model and
validator; this plan covers what those tests cannot: the actual Windows service
installation and the setting written to `ServiceControl.Audit.exe.config`.

Scenarios are independent of each other and can be executed together (mob-style),
split among testers, or run async. Each scenario states its own preconditions, so any
tester can bring a machine into the required state without running previous scenarios.

---

## Shared machine setup (once per test machine)

All scenarios need this baseline. If you split scenarios among testers on different
machines, each machine needs this setup.

1. **Windows machine or VM** with local administrator rights (SCMU requires elevation
   to install Windows services).
2. **Build and launch the SCMU under test** from this branch:
   - Build `ServiceControl.Config` (or use the packaged installer produced from the branch).
   - Run it elevated.
3. **A working transport** available to the machine, so instances can actually be
   installed and started. Any supported transport works; suggested low-friction options:
   - RabbitMQ in a local Docker container, or
   - SQL Server / PostgreSQL transport against a local instance.
   Keep the connection string at hand; every scenario that installs an instance needs it.
4. **Snapshot/cleanup strategy:** scenarios require specific numbers of pre-installed
   error instances (0, 1, or 2). Between scenarios, remove instances via SCMU
   (instance → Remove, tick "delete database" and "delete logs") or restore a VM
   snapshot of the clean baseline. Removal order does not matter for this plan.

### How to verify the written setting (used by several scenarios)

1. Open the audit instance's install path (visible on the instance card in SCMU),
   e.g. `C:\ProgramData\Particular\ServiceControl\<instance name>\` or the destination
   path chosen during install.
2. Open `ServiceControl.Audit.exe.config`.
3. Locate `<add key="ServiceControl.Audit/ServiceControlQueueAddress" value="..." />`.
4. The `value` must be exactly the expected error instance name for the scenario.
5. Additionally verify the audit instance **starts and stays running** (SCMU shows it
   running; the instance log contains no
   ["no destination specified for message"](https://docs.particular.net/servicecontrol/troubleshooting#no-destination-specified-for-message)
   errors).

### Terminology

- **Error instance** = a "ServiceControl" instance (the SCMU add screen offers
  "ServiceControl" and "ServiceControl Audit"; the first is referred to as the error
  instance in the UI validation messages).
- **Add screen** = SCMU → New → "Add ServiceControl and Audit Instances".

---

## Scenario matrix

| # | Rule | Scenario | Pre-installed error instances required |
|---|------|----------|----------------------------------------|
| 1 | Rule 1 | Install both together → audit points at the new error instance | 0 |
| 2 | Rule 1 (counter) | Install both together with existing error instances → new error instance still wins, no dropdown | 2 |
| 3 | Rule 2 | Add audit alone, one existing error instance → auto-detected, no dropdown | 1 |
| 4 | Rule 3 | Add audit alone, two existing error instances → dropdown offers both | 2 |
| 5 | Rule 3 | Save blocked until a dropdown choice is made, unblocked after | 2 |
| 6 | Rule 4 | Add audit alone, no error instance → warning shown, install blocked | 0 |
| 7 | Rule 4 (counter) | Add error instance alone → no queue-address validation raised | 0 |

Suggested split for three testers: A = 1, 2 (both-together flows); B = 3, 6, 7
(auto-detect and blocking); C = 4, 5 (multiple-instance dropdown). Scenarios 2, 4 and 5
share the same precondition (two error instances installed), so grouping them on one
machine saves setup time.

---

## Scenario 1 — Rule 1: both instances installed together

**Rule:** Must address the audit instance to the error instance installed in the same session.

**Preconditions**
- No ServiceControl (error) or audit instances installed on the machine.
- Verify: SCMU instance list is empty.

**Steps**
1. Open the Add screen.
2. Keep **both** checkboxes ticked (ServiceControl and ServiceControl Audit).
3. Note the error instance name (default `Particular.ServiceControl`).
4. Fill in transport, connection string, ports, and paths as needed.
5. Observe the audit section: no "ERROR INSTANCE" dropdown and no red
   "No error instance was found..." warning should be visible.
6. Click Add and let the installation finish.

**Pass criteria**
- [ ] No dropdown and no warning were shown in the audit section.
- [ ] Installation completes; both instances appear in SCMU and start.
- [ ] `ServiceControl.Audit.exe.config` contains
      `ServiceControl.Audit/ServiceControlQueueAddress` = the error instance name from step 3.
- [ ] Audit instance runs with no "no destination specified for message" log errors.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 2 — Rule 1 counter-example: existing instances do not win over the one being installed

**Rule:** When installing both together, the error instance being installed always wins — never an already-installed one.

**Preconditions**
- **Two** error instances already installed (e.g. `Particular.ServiceControl.A` and
  `Particular.ServiceControl.B`), no audit instances.
- Tip: install them one at a time via the Add screen with only the ServiceControl
  checkbox ticked, using distinct names/ports.
- Verify: SCMU lists exactly two error instances.

**Steps**
1. Open the Add screen.
2. Keep both checkboxes ticked.
3. Give the new error instance a distinct name (e.g. `Particular.ServiceControl.New`).
4. Observe the audit section: even though two error instances exist, **no**
   "ERROR INSTANCE" dropdown should be shown (the instance being installed wins).
5. Fill in the remaining fields and click Add.

**Pass criteria**
- [ ] No dropdown was shown while both checkboxes were ticked.
- [ ] `ServiceControl.Audit.exe.config` contains
      `ServiceControlQueueAddress` = `Particular.ServiceControl.New` (the new instance,
      not `...A` or `...B`).
- [ ] Audit instance starts and stays running.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 3 — Rule 2: auto-detect single existing error instance

**Rule:** Should auto-detect the existing error instance when adding an audit instance alone.

**Preconditions**
- Exactly **one** error instance installed (note its name, e.g. `Particular.ServiceControl`),
  no audit instances.
- Verify: SCMU lists exactly one error instance.

**Steps**
1. Open the Add screen.
2. **Untick** the ServiceControl checkbox; keep only ServiceControl Audit ticked.
3. Observe the audit section: no "ERROR INSTANCE" dropdown and no warning should be
   visible — detection is silent.
4. Fill in the remaining fields and click Add.

**Pass criteria**
- [ ] No dropdown and no warning were shown; Save was not blocked by the queue address.
- [ ] `ServiceControl.Audit.exe.config` contains
      `ServiceControlQueueAddress` = the name of the pre-existing error instance.
- [ ] Audit instance starts and stays running.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 4 — Rule 3: dropdown offers all detected error instances

**Rule:** Must require an explicit choice when multiple existing error instances are found.

**Preconditions**
- **Two** error instances installed (e.g. `Particular.ServiceControl.A`,
  `Particular.ServiceControl.B`), no audit instances.
- Verify: SCMU lists exactly two error instances.

**Steps**
1. Open the Add screen.
2. Untick the ServiceControl checkbox; keep only ServiceControl Audit ticked.
3. Locate the "ERROR INSTANCE" dropdown in the audit GENERAL section.
4. Open the dropdown and inspect its items.

**Pass criteria**
- [ ] The dropdown is visible (only in this configuration — see also scenarios 1-3
      where it must be hidden).
- [ ] The dropdown contains exactly the two installed instance names, no more, no less.
- [ ] No instance is silently pre-selected on first open (no guess is made for the user).

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 5 — Rule 3: Save blocked until a choice is made

**Rule:** Save is blocked until the user picks one of the detected instances, and unblocked once a choice is made.

**Preconditions**
- Same as Scenario 4 (two error instances, no audit instances). Can be run in the same
  session as Scenario 4.

**Steps**
1. Open the Add screen; untick ServiceControl, keep ServiceControl Audit ticked.
2. Fill in **all other** required audit fields (name, transport, connection string,
   ports, paths, service account) but leave the "ERROR INSTANCE" dropdown unselected.
3. Click Add.
4. Expect a validation error on the dropdown; the installation must not start.
5. Select one of the two instances in the dropdown (note which one, e.g.
   `Particular.ServiceControl.B`).
6. Click Add again and let the installation finish.

**Pass criteria**
- [ ] Step 3: install did not proceed; the validation error
      "An existing error instance must be selected for the audit instance to send messages to"
      was raised against the dropdown.
- [ ] Step 6: install proceeded after choosing.
- [ ] `ServiceControl.Audit.exe.config` contains `ServiceControlQueueAddress` = the
      instance chosen in step 5.
- [ ] Audit instance starts and stays running.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 6 — Rule 4: install blocked when no error instance exists

**Rule:** Must block installation when no error instance exists to connect to.

**Preconditions**
- **No** error instances installed (audit instances, if any, removed as well for a
  clean read of the instance list).
- Verify: SCMU instance list contains no error instances.

**Steps**
1. Open the Add screen.
2. Untick the ServiceControl checkbox; keep only ServiceControl Audit ticked.
3. Observe the audit GENERAL section.
4. Fill in all required audit fields and click Add.

**Pass criteria**
- [ ] The red warning is shown: "No error instance was found on this machine. The
      audit instance needs one to send messages to. Also install the error instance
      above, or add one first."
- [ ] No "ERROR INSTANCE" dropdown is shown (there is nothing to choose from).
- [ ] Clicking Add does not install anything; a validation error blocks the save.
- [ ] Bonus check: ticking the ServiceControl checkbox again makes the warning
      disappear (the error instance in the same session satisfies the requirement).

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 7 — Rule 4 counter-example: error-instance-only install is unaffected

**Rule:** When only an error instance is being installed, the queue address does not apply and no validation error is raised.

**Preconditions**
- No instances installed.
- Verify: SCMU instance list is empty.

**Steps**
1. Open the Add screen.
2. Untick the ServiceControl **Audit** checkbox; keep only ServiceControl ticked.
3. Fill in the error instance fields and click Add.

**Pass criteria**
- [ ] No queue-address validation error appears anywhere in the flow.
- [ ] The error instance installs and starts normally.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Wrap-up

- Collect results per scenario (pass/fail, tester, notes) in this file or a copy.
- Any failure: capture a screenshot of the Add screen, the SCMU log, and the
  `ServiceControl.Audit.exe.config` (if written), and link them in the notes.
- Known out-of-scope behavior (do **not** log as failures):
  - The new audit instance is *not* registered as a remote of a pre-existing error
    instance (`AddRemoteInstance` only runs when both are installed together) —
    tracked as a follow-up candidate in the spec.
  - PowerShell (`New-ServiceControlAuditInstance`) already enforces the parameter as
    mandatory and is not covered by this plan.
