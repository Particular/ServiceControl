# Primary instance acceptance test review

A review of the primary instance's acceptance tests for assertion correctness, prioritised by what
ServicePulse actually calls.

## Why this is worth doing

Two tests in this suite were recently found to be testing nothing at all. Both registered a test
double with `AddSingleton<ConcreteType>()` while the code under test injects
`IEnumerable<IEnrichImportedErrorMessages>`, so the double was never resolved and never ran. Both
compiled, both passed, and both had passed for years.

That is the shape of the problem: these failures are silent. A test that asserts nothing and a test
that asserts something unfalsifiable both look identical to CI. The purpose of this review is to find
the rest of them before the EF persistence work starts leaning on this suite as its safety net.

## Scope

In scope: the functional areas of `ServiceControl.AcceptanceTests`, being `Recoverability`,
`Monitoring`, `EventLogs` and `WebApi`. That is 71 `When_*.cs` files.

Out of scope: `Security/*` (25 files across ForwardedHeaders, OpenIdConnect, Cors and Https), which
carries a different risk model and deserves its own pass. Also out of scope: the audit instance,
monitoring instance, and multi-instance suites, except where they already cover a route the primary
suite misses.

Three artefacts anchor the work:

- `src/ServiceControl.UnitTests/ApprovalFiles/APIApprovals.HttpApiRoutes.approved.txt` inventories 77
  routes. It is **not** the whole primary-instance surface: the approval test behind it scans only
  `typeof(Program).Assembly` and `typeof(MyRoutesController).Assembly`.
- `src/Particular.LicensingComponent.UnitTests/ApprovalFiles/APIApprovals.HttpApiRoutes.approved.txt`
  inventories the other 8, served under the `api/licensing` prefix, via a separate approval test
  scanning `typeof(ThroughputCollector).Assembly`. Anything reasoning about "the API surface" has to
  read both files or it will silently miss the throughput and licensing endpoints.
- `src/composables/apiRoutes.ts` in ServicePulse maps each gated UI capability to the route behind
  it and names the ServiceControl controller for each. It describes itself as the only place
  coupling ServicePulse to ServiceControl's route surface.

That makes 85 routes in total.

## Defect patterns

Each of these was found in the current suite. The value of naming them is that each becomes a
repeatable check to run over all 71 files, rather than a one-off fix.

### 1. Test double registered against the wrong service type (silent)

The double is registered as its concrete type, so the collection the production code injects never
contains it. The test still passes, having exercised none of the behaviour it names. Found twice:
`CounterEnricher` in `When_errors_with_same_uniqueid_are_imported` and `FailOnceEnricher` in
`When_single_message_fails_in_batch`. Both are fixed; the pattern is not.

**Detect:** for every `AddSingleton<T>()` in a test, assert T is resolved by the component under
test. A convention test over the test assembly can do this: resolve the host and assert each
registered double appears in the collection its interface feeds.

### 2. No assertion, failure surfaces only as a timeout (diagnostics)

Seven of the 71 files contain no `Assert` at all. Five use the `Do("step", …)` sequence helper, which
logs `Advancing from X to Y` on each transition, so a regression there is diagnosable from the
console output. That is a deliberate and acceptable pattern. The remaining two gate on a single
`Done` predicate and report a regression as a bare 90-second timeout with nothing to read.

**Detect:** files matching `When_*.cs` with zero `Assert.` occurrences, minus those using the
`Sequence` helper. Currently 2.

### 3. Assertion restates the condition the scenario already waited on (unfalsifiable)

`When_a_invalid_id_is_sent_to_retry` ends with `Assert.That(context.Done, Is.True)` after
`.Done(ctx => ctx.Done)`. The assertion cannot fail: if the flag were false the scenario would have
timed out first. The real subject of that test, that posting a retry for a non-existent id does not
break subsequent batches, is never asserted, and the response of the invalid POST is never
inspected.

**Detect:** a mechanical grep found 8 candidates of the shape `Assert.That(context.Flag, Is.True)`.
One is confirmed tautological; the rest need reading individually, because a flag set by a message
handler and gated on something else is legitimate.

### 4. Assertions coupled to one persister's internals (portability)

Tests that assert RavenDB implementation details rather than the contract both persisters offer. The
multi-attempt tests asserted Raven's ten-attempt trimming and its full attempt history, neither of
which EF provides by design. These read as EF gaps when they are really over-specified tests, and
they are the main reason the EF exclusion list looks longer than the real feature gap.

**Detect:** the `<Compile Remove>` blocks in the two EF acceptance csprojs are the existing
inventory. Each entry is either a real gap, a settled design difference, or an over-specified test,
and the comments do not currently distinguish them.

### 5. Setup computed but never asserted (rot)

Fixtures, headers and constants that exist to support an assertion that was removed or never
written: a `Counter` header recorded into a dictionary nothing reads, a
`MaximalNumberOfStoredFailedAttempts` constant, a list of failure times trimmed to a window that is
then discarded. Harmless on its own, but it makes tests read as though they cover more than they do,
which is how the first two patterns survive review.

**Detect:** per-file reading. Context properties with a setter and no read outside the scenario are
the strongest signal.

## The routes that need tests

40 of the 85 routes are called by no acceptance test in any suite. This is the whole list, confirmed
route by route against the two approved route lists and the test sources, and it is the work rather
than a sample of it. Ticking every box here is what closes phase 4.

The groups are sized to be one PR each and ordered by what breaks in ServicePulse if the route
regresses. Each entry names the ServicePulse consumer where there is one, because that is what the
test should assert: the contract the UI relies on, not a 200.

### Nav-gating routes

If one of these regresses, ServicePulse loses a whole section of its navigation and nothing in the
suite notices.

- [ ] `GET /api/heartbeats/stats`: gates the Heartbeats nav item (`viewHeartbeats`)
- [ ] `GET /api/messages2`: gates the audit messages nav item (`viewAuditMessages`)
- [ ] `GET /api/license`: gates the Licence nav item (`viewLicense`)

`GET /api/licensing/report/available` gates the Throughput nav item and belongs to this tier too,
but it is written with the rest of the licensing block below. `GET /api/connection` gates the
Connections nav item and is covered in MultiInstance only, so it is a home decision rather than a
new test.

### Licensing and throughput

All eight `api/licensing` routes, one PR. The largest single win: the throughput *domain logic*
beneath them is the best-unit-tested area in the codebase, with a dedicated
`Particular.LicensingComponent.UnitTests` project carrying report generation, masking, date handling
and summary indicator tests plus approval files. None of it touches `LicensingController`, so the
HTTP surface, its serialisation and its authorisation are untested end to end. Check first whether
the acceptance test host even starts the licensing component, as that decides whether this is a new
test file or a new test project.

- [ ] `GET /api/licensing/report/available`: gates the Throughput nav item (`viewThroughput`)
- [ ] `POST /api/licensing/settings/masks/update`: `manageThroughput`
- [ ] `GET /api/licensing/settings/masks`: throughput masking settings
- [ ] `GET /api/licensing/settings/test`: connection test on the throughput settings page
- [ ] `GET /api/licensing/settings/info`: throughput settings summary
- [ ] `GET /api/licensing/report/file`: downloads the throughput report
- [ ] `GET /api/licensing/endpoints`: endpoint throughput list
- [ ] `POST /api/licensing/endpoints/update`: saves per-endpoint user indicators

### Notifications

- [ ] `GET /api/notifications/email`: `viewNotifications`
- [ ] `POST /api/notifications/email`: `manageNotifications`
- [ ] `POST /api/notifications/email/test`: `testNotifications`
- [ ] `POST /api/notifications/email/toggle`: the enable/disable switch

### Actions ServicePulse offers on messages and groups

- [ ] `PATCH/POST /api/errors/archive`: `deleteMessage`, the batch delete. `PATCH /api/errors/{id}/archive` is well covered, so the single-message route passing has been standing in for a batch route no test calls
- [ ] `POST /api/recoverability/groups/{id}/errors/unarchive`: `restoreGroup`
- [ ] `DELETE /api/customchecks/{id}`: `dismissCustomCheck`. Same shape as heartbeats: `GET /api/customchecks` is covered and the dismiss route beside it is not
- [ ] `DELETE /api/recoverability/unacknowledgedgroups/{id}`: dismissing completed group operations

### Endpoint settings

- [ ] `PATCH /api/endpointssettings/{name}`: `manageEndpointSettings`
- [ ] `GET /api/endpointssettings`: the settings list the PATCH edits

### Failed message and group queries

- [ ] `GET /api/errors/summary`: failed-message summary counts
- [ ] `GET /api/recoverability/history`: retry history panel
- [ ] `POST /api/recoverability/groups/{id}/comment`: group comments
- [ ] `DELETE /api/recoverability/groups/{id}/comment`: group comments
- [ ] `GET /api/recoverability/groups/id/{groupId}`: single group. The archive twin, `GET /api/archive/groups/id/{groupId}`, is covered
- [ ] `HEAD /api/recoverability/groups/{id}/errors`: the count behind group paging
- [ ] `GET /api/endpoints/{name}/errors`: failed messages filtered to one endpoint
- [ ] `HEAD /api/errors`: the count behind failed-message paging

### Retry and resolve routes

- [ ] `POST /api/errors/queues/{queueAddress}/retry`: retry everything for a queue
- [ ] `PATCH /api/pendingretries/queues/resolve`: resolve pending retries by queue
- [ ] `PATCH /api/errors/{from}...{to}/unarchive`: unarchive by date range

### Audit-backed message queries

These need an audit instance, so decide whether MultiInstance is the right home before adding them
to the primary suite.

- [ ] `GET /api/messages/search`: the `?q=` form. The `/search/{keyword}` form is covered
- [ ] `GET /api/endpoints/{endpoint}/messages/search`: the same, scoped to an endpoint
- [ ] `GET /api/endpoints/{endpoint}/audit-count`: audit counts per endpoint

### Configuration surface

- [ ] `GET /api/configuration`: only `/api/configuration/remotes` is covered today
- [ ] `GET /api/instance-info`: same action as `/api/configuration`, separate route
- [ ] `GET /api/edit/config`: the edit-and-retry feature flag ServicePulse reads before offering edit
- [ ] `GET /api/license/details`
- [ ] `POST /api/license/detailsUpload`

### Covered only in MultiInstance

Not gaps, but not in the primary suite either. Decide deliberately whether MultiInstance is the
right home before duplicating any of them.

- [ ] `GET /api/connection`: gates the Connections nav item (`viewConnections`)
- [ ] `GET /api/endpoints/known`: known endpoints list, covered by `When_endpoint_known_to_audit_instance`
- [ ] `GET /api/conversations/{id}`: sequence diagram
- [ ] `GET /api/sagas/{id}`: saga diagram
- [ ] `GET /api/endpoints/{endpoint}/messages`
- [ ] `GET /api/endpoints/{endpoint}/messages/search/{keyword}`

Worth being precise about the heartbeats entry, because several others are the same shape. Heartbeat
*ingestion* is well covered: six tests drive endpoints starting up, going quiet, and being marked
monitored. What no test calls is `/api/heartbeats/stats`, the endpoint ServicePulse's heartbeats page
actually reads. The plumbing is tested; the contract on top of it is not.

## The work

### Phase 1: confirm the gap list properly (done)

The original table was grep-based, and a route reached through a helper, a constant or an
interpolated base path would have been missed. The list above replaces it, built from both approved
route lists rather than from greps and checked route by route against the primary and MultiInstance
sources. Reading only the ServiceControl route list is what hid the licensing routes in the first
place, so both were read.

Two details mattered while confirming it. The approved lists carry the action-level template only,
so they hold two rows both reading `GET /configuration`, one on `RootController` under `api` and one
on `AuthenticationController` under `api/authentication`; the controller-level `[Route]` has to be
folded in or the two merge. And the verb has to be tracked separately from the path, because
ServicePulse gates `GET` and `POST` on `/api/notifications/email` as two different capabilities.

Six entries changed as a result. `GET /api/endpoints/known` is not a gap: MultiInstance covers it.
The other five were gaps the grep missed, and four of them gate a ServicePulse capability:
`GET /api/license` and `GET /api/connection` gate nav items, and `DELETE /api/customchecks/{id}`,
`PATCH/POST /api/errors/archive` and `POST /api/recoverability/groups/{id}/errors/unarchive` are
actions the UI offers.

No tooling came out of this phase, deliberately. A test that scans the suite's source to police its
own coverage is a second thing to maintain and gets stale in its own way; the list above is the
artefact, and new routes are a review-time concern.

### Phase 2: the silent-registration class (done)

The whole suite registers services from a test in seven places, so the audit was exhaustive rather
than a sample. Five were correct. Two were the known `IEnrichImportedErrorMessages` cases, already
fixed. One was new:

`When_a_critical_error_is_triggered` registered `CriticalErrorCustomCheck` as its own concrete type,
with a comment saying it overrode the production registration to shorten the check interval. It did
not. The check is registered with `TryAddEnumerable` against `ICustomCheck` and consumed through
`GetServices<ICustomCheck>()`, so the test's registration was never resolved and the check ran on
its 60-second production interval. The test passed either way, about a minute slower than intended.
It now removes the production registration explicitly and re-adds the check against `ICustomCheck`,
and runs in six seconds.

One registration is worth knowing about even though it is correct. `When_a_retry_fails_to_be_sent`
substitutes a `FakeReturnToSender` by re-registering `ReturnToSender`, which works only because
`CustomizeHostBuilder` runs after all production registration and `ReturnToSenderDequeuer` resolves
a single instance rather than a collection. That is a real distinction, not a detail: the same move
against a collection adds a second implementation and leaves the production one running.

No harness check came out of this phase. The underlying fault is a test written so that it could
pass without its own setup taking effect, and a convention test policing registrations would catch
one shape of that while leaving the rest. The practice is written down instead, in
[Writing acceptance tests](writing-acceptance-tests.md), which covers registering against the
injected abstraction, replacing a production registration so that it fails loudly if production
moves, and asserting on evidence the double actually ran.

### Phase 3: sweep the 71 files, one area per PR

Read for the five patterns above, area by area, so each PR stays reviewable and the diff maps to one
owner's mental model. Fix what is cheap to fix in the same PR; raise anything that changes what a
test means as its own change with the reasoning written down.

- `Recoverability/MessageFailures`, 22 files, the densest area and the one the EF work touches most.
- `Recoverability/*` root, `Groups`, `MessageRedirects`: the retry, group and redirect flows.
- `Monitoring/*` and `EventLogs`: heartbeats, custom checks, endpoint monitoring.
- `Recoverability/ExternalIntegration` and `Monitoring/ExternalIntegration`: hold until the EF
  external-integration work lands, then review against both persisters at once.

### Phase 4: work through the route list

Straight down the list in "The routes that need tests", one PR per group, in the order the groups
are given. The phase is done when every box is ticked.

Each test asserts the contract its consumer relies on rather than just a 200: the shape ServicePulse
reads, the status it branches on, the effect the action has on the next request. A test that only
proves the route is routable would leave the same gap in a different form.

## What this plan does not do

It does not keep the route list current by machine. The list is a snapshot taken during phase 1, and
a route added after that will not appear in it on its own. Catching those is a review-time concern:
a new controller action arrives with the PR that adds it, which is where the test for it belongs.

It does not review the `Security/*` tests, the audit instance, or the monitoring instance.

It does not treat persister parity as a workstream in its own right. It appears only as one defect
pattern, on the grounds that most of the current EF exclusions are over-specified tests rather than
missing features, which is itself a claim worth confirming during phase 3.
