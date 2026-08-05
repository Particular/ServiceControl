# Manual Test Plan: SCMU resilience to corrupt instance configuration files

> [!WARNING]
> **This document is a working artifact for the manual validation of this fix and
> is meant to be DELETED before merging the PR.** Record the test results here (or in
> a copy) while the PR is open.

**Spec:** [scmu-corrupt-instance-configuration.md](scmu-corrupt-instance-configuration.md)
**Bug:** [#2759 — SCMU: Fails to start when one of the instances has a corrupt configuration file](https://github.com/Particular/ServiceControl/issues/2759)

This plan validates, end-to-end on a real machine, the rules implemented for SCMU's
handling of corrupt instance configuration files. The automated executable
specification covers the view models over real config files; this plan covers what
those tests cannot:

- **Instance enumeration against real Windows services** (`InstanceFinder` is static
  and machine-dependent — a mutation check confirmed the automated suite cannot
  detect a reintroduced silent skip there).
- **The XAML banner wiring** (per-instance and list-level banners — the same mutation
  check confirmed removing the list banner from the view goes undetected).
- **The actual SCMU startup path** — the original bug is a crash at startup, which no
  unit-level test exercises.

Scenarios are independent of each other and can be executed together (mob-style),
split among testers, or run async. Each scenario states its own preconditions, so any
tester can bring a machine into the required state without running previous scenarios.

---

## Shared machine setup (once per test machine)

All scenarios need this baseline. If you split scenarios among testers on different
machines, each machine needs this setup.

1. **Windows machine or VM** with local administrator rights (SCMU requires elevation
   to manage Windows services).
2. **Build and launch the SCMU under test** from this branch:
   - Build `ServiceControl.Config` (or use the packaged installer produced from the branch).
   - Run it elevated.
3. **At least one error (ServiceControl) instance installed.** Scenarios state when
   they additionally need an audit and/or monitoring instance. Any supported transport
   works; suggested low-friction options:
   - RabbitMQ in a local Docker container, or
   - SQL Server / PostgreSQL transport against a local instance.
4. **Snapshot/cleanup strategy:** corruption is introduced by editing config files, so
   always take the backup described below before corrupting, and restore it when the
   scenario is done. For instance add/remove needs, remove instances via SCMU
   (instance → Remove) or restore a VM snapshot of the clean baseline.

### How to corrupt a configuration file (used by every scenario)

1. Find the instance's install path (visible on the instance card in SCMU), e.g.
   `C:\Program Files (x86)\Particular Software\<instance name>\`.
2. The config file per instance type:
   - error instance: `ServiceControl.exe.config`
   - audit instance: `ServiceControl.Audit.exe.config`
   - monitoring instance: `ServiceControl.Monitoring.exe.config`
3. **Back it up first**: copy it next to itself as `<file>.bak`.
4. Corrupt it by making the XML invalid — e.g. delete the closing
   `</configuration>` tag, or paste `<<<` in the middle of the file.
5. To repair: copy the `.bak` back over the corrupted file.

The Windows service itself does not need to be stopped; SCMU reads the file directly.

### Terminology

- **Error instance** = a "ServiceControl" instance; **audit** and **monitoring** refer
  to "ServiceControl Audit" and "ServiceControl Monitoring" instances.
- **DEPLOYED INSTANCES screen** = SCMU's main instance list.
- **Per-instance banner** = the error banner on the instance card; **summary banner**
  = the banner at the top of the DEPLOYED INSTANCES screen.

---

## Scenario matrix

| # | Rule | Scenario | Instances required |
|---|------|----------|--------------------|
| 1 | Rule 1 | SCMU starts with a corrupt error instance config and lists every instance (the original crash) | 2 error |
| 2 | Rule 1 | Corrupt audit and monitoring configs are handled the same way | 1 audit, 1 monitoring |
| 3 | Rule 2 | Status reads CONFIGURATION ERROR and the banner names the file | 1 error |
| 4 | Rule 3 | Actions requiring a valid configuration are blocked | 1 error |
| 5 | Rule 5 | Summary banner above the list names the corrupt instance(s) | 2 error |
| 6 | Rule 4 | Fix on disk + refresh recovers the instance without restarting SCMU | 1 error |
| 7 | Rule 4 | Corruption introduced while SCMU is running is flagged on refresh | 1 error |

Suggested split for three testers: A = 1, 2 (startup/enumeration); B = 3, 4
(per-instance UI state); C = 5, 6, 7 (summary banner and the refresh loop). Scenarios
1 and 5 share the same precondition (two error instances), so grouping them on one
machine saves setup time.

---

## Scenario 1 — Rule 1: SCMU starts with a corrupt config and lists every instance

**Rule:** Must load every installed instance even when its configuration file is corrupt.
This is the original bug: before the fix, SCMU crashed at startup.

**Preconditions**
- **Two** error instances installed (e.g. `Particular.ServiceControl` and
  `Particular.ServiceControl.2`).
- SCMU **not** running.

**Steps**
1. Corrupt the config of **one** of the two error instances (back it up first).
2. Start SCMU.
3. Observe the DEPLOYED INSTANCES screen.

**Pass criteria**
- [ ] SCMU starts — no crash, no fatal error dialog.
- [ ] **Both** instances appear in the list — the corrupt one is flagged, not missing
      (this is the silent-skip check automation cannot perform).
- [ ] The corrupt instance's display name falls back to its Windows service name.
- [ ] The healthy instance shows its normal service status and is unaffected.
- [ ] Restore the backup, refresh, and confirm both instances show as healthy.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 2 — Rule 1: audit and monitoring instances are protected the same way

**Rule:** All three instance types load in the error state when their config is corrupt.

**Preconditions**
- One audit instance and one monitoring instance installed (an error instance may
  also be present).
- SCMU **not** running.

**Steps**
1. Corrupt the audit instance's `ServiceControl.Audit.exe.config` (back it up first).
2. Corrupt the monitoring instance's `ServiceControl.Monitoring.exe.config` (back it up first).
3. Start SCMU and observe the list.

**Pass criteria**
- [ ] SCMU starts; both the audit and the monitoring instance appear in the list,
      each flagged with a configuration error.
- [ ] Any other (healthy) instances are unaffected.
- [ ] Restore both backups, refresh, and confirm both instances return to normal.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 3 — Rule 2: the error is shown in place of the service status and names the file

**Rule:** Must show the configuration error in place of the service status.

**Preconditions**
- One error instance installed; SCMU may already be running.

**Steps**
1. Corrupt the error instance's config (back it up first).
2. Refresh SCMU (or start it) and locate the instance card.

**Pass criteria**
- [ ] The status line reads `CONFIGURATION ERROR` instead of the Windows service status.
- [ ] Neither the running nor the stopped indicator is shown.
- [ ] The per-instance banner says the configuration failed to load **and names the
      full path of the config file** to fix (the exact file edited in step 1).
- [ ] Restore the backup, refresh: the normal service status returns and the banner
      disappears.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 4 — Rule 3: actions requiring a valid configuration are blocked

**Rule:** Must block actions that require a valid configuration while the error persists.

**Preconditions**
- Same as Scenario 3 (one error instance, corrupt config). Can be run in the same
  session as Scenario 3.

**Steps**
1. With the instance in the CONFIGURATION ERROR state, inspect its card.
2. Attempt every action the card offers.

**Pass criteria**
- [ ] Start and Stop are not available.
- [ ] Edit and Advanced Options are hidden (or not invocable).
- [ ] No transport or persister is reported on the card.
- [ ] Remove is still possible? Note the observed behavior — removal of a broken
      instance is the operator's last resort; record what happens in Notes.
- [ ] Restore the backup, refresh: Edit/Advanced Options/Start/Stop become available
      again and transport/persister are reported.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 5 — Rule 5: summary banner above the instance list

**Rule:** Must summarize configuration errors above the instance list.
This is the XAML wiring the automated suite cannot see (confirmed surviving mutant).

**Preconditions**
- **Two** error instances installed. Can be run together with Scenario 1.

**Steps**
1. Corrupt the config of **one** instance (back it up first); refresh SCMU.
2. Observe the top of the DEPLOYED INSTANCES screen.
3. Corrupt the **second** instance's config too (back it up first); refresh.
4. Restore **both** backups; refresh.

**Pass criteria**
- [ ] Step 2: a summary banner is visible **above** the instance list and names the
      corrupt instance exactly.
- [ ] Step 3: the banner lists **both** corrupt instances.
- [ ] Step 4: the banner disappears entirely once all configurations are valid.
- [ ] The banner is readable and visually distinct (warning styling, not lost in the
      layout) — attach a screenshot to the notes.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 6 — Rule 4: fix on disk + refresh recovers the instance without restarting SCMU

**Rule:** Should recover the instance once the configuration file is fixed and the
list is refreshed.

**Preconditions**
- One error instance installed, config corrupted (back it up first), SCMU running and
  showing the CONFIGURATION ERROR state.

**Steps**
1. Repair the config file on disk (copy the `.bak` back).
2. Do **not** restart SCMU — trigger the DEPLOYED INSTANCES refresh.
3. Observe the instance card and the top of the list.

**Pass criteria**
- [ ] The instance returns to normal: real service status shown, Edit allowed,
      transport/persister reported.
- [ ] Both the per-instance banner and the summary banner disappear.
- [ ] SCMU was **not** restarted at any point in the fix loop.

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Scenario 7 — Rule 4: corruption introduced while SCMU is running is flagged on refresh

**Rule:** Refresh re-reads the configuration from disk — in both directions.

**Preconditions**
- One error instance installed with a **valid** config, SCMU running and showing the
  instance as healthy.

**Steps**
1. With SCMU still running, corrupt the instance's config (back it up first).
2. Trigger the DEPLOYED INSTANCES refresh.
3. Observe the instance card and the top of the list.

**Pass criteria**
- [ ] The instance flips to CONFIGURATION ERROR; the banner names the config file.
- [ ] The summary banner appears above the list.
- [ ] Restore the backup, refresh again: everything returns to normal (full loop).

**Result:** ☐ Pass ☐ Fail — Tester: ______ Date: ______ Notes: ______

---

## Wrap-up

- Collect results per scenario (pass/fail, tester, notes) in this file or a copy.
- Any failure: capture a screenshot of the DEPLOYED INSTANCES screen, the SCMU log,
  and the corrupted config file, and link them in the notes.
- Known out-of-scope behavior (do **not** log as failures):
  - Corruption other than invalid XML (e.g. valid XML with missing/invalid setting
    values) may surface lazily at first read rather than at load; the spec covers
    both paths, but only invalid XML is exercised by this plan.
  - The Windows service itself may keep running with a corrupt config on disk (it
    read its settings at startup) — SCMU flagging the file while the service runs is
    correct behavior, not a contradiction.
- The PowerShell module fallback mentioned in the bug report is unaffected by this
  fix and is not covered by this plan.
