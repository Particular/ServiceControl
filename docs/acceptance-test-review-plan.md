# Primary instance acceptance test review

A review of the primary instance's acceptance tests for assertion correctness, prioritised by what ServicePulse actually calls.

## Why this is worth doing

Two tests in this suite were recently found to be testing nothing at all. Both registered a test double with `AddSingleton<ConcreteType>()` while the code under test injects `IEnumerable<IEnrichImportedErrorMessages>`, so the double was never resolved and never ran. Both compiled, both passed, and both had passed for years.

That is the shape of the problem: these failures are silent. A test that asserts nothing and a test that asserts something unfalsifiable both look identical to CI. The purpose of this review is to find the rest of them before the EF persistence work starts leaning on this suite as its safety net.

## Where this has got to

Phases 1 and 2 are done. Phase 3 has swept the first of its four areas, `Recoverability/MessageFailures`. Phase 4 has closed the licensing block, 8 routes of the 40, on both of the branches those routes take.

What is left: three areas to sweep, of which the two `ExternalIntegration` ones are parked until the EF work lands, and 32 routes to cover.

## Scope

In scope: the functional areas of `ServiceControl.AcceptanceTests`, being `Recoverability`, `Monitoring`, `EventLogs` and `WebApi`. That is 71 `When_*.cs` files.

Out of scope: `Security/*` (25 files across ForwardedHeaders, OpenIdConnect, Cors and Https), which carries a different risk model and deserves its own pass. Also out of scope: the audit instance, monitoring instance, and multi-instance suites, except where they already cover a route the primary suite misses.

Three artefacts anchor the work:

- `src/ServiceControl.UnitTests/ApprovalFiles/APIApprovals.HttpApiRoutes.approved.txt` inventories 77 routes. It is **not** the whole primary-instance surface: the approval test behind it scans only `typeof(Program).Assembly` and `typeof(MyRoutesController).Assembly`.
- `src/Particular.LicensingComponent.UnitTests/ApprovalFiles/APIApprovals.HttpApiRoutes.approved.txt` inventories the other 8, served under the `api/licensing` prefix, via a separate approval test scanning `typeof(ThroughputCollector).Assembly`. Anything reasoning about "the API surface" has to read both files or it will silently miss the throughput and licensing endpoints.
- `src/composables/apiRoutes.ts` in ServicePulse maps each gated UI capability to the route behind it and names the ServiceControl controller for each. It describes itself as the only place coupling ServicePulse to ServiceControl's route surface.

That makes 85 routes in total.

## Defect patterns

Each of these was found in the current suite. The value of naming them is that each becomes a repeatable check to run over all 71 files, rather than a one-off fix.

### 1. Test double registered against the wrong service type (silent)

The double is registered as its concrete type, so the collection the production code injects never contains it. The test still passes, having exercised none of the behaviour it names. Found three times: `CounterEnricher` in `When_errors_with_same_uniqueid_are_imported`, `FailOnceEnricher` in `When_single_message_fails_in_batch`, and `CriticalErrorCustomCheck` in `When_a_critical_error_is_triggered`, which phase 2 turned up. All three are fixed.

**Detect:** read how the collaborator takes its dependency before registering anything, since a service resolved as a collection is added to rather than replaced. Phase 2 audited all seven registrations in the suite and settled on writing the practice down in [Writing acceptance tests](writing-acceptance-tests.md) rather than building a check, because the fault is a test that passes without its own setup taking effect and a convention test would catch one shape of that.

### 2. No assertion, failure surfaces only as a timeout (diagnostics)

Seven files contain no `Assert` at all. Five use the `Do("step", …)` sequence helper, which logs `Advancing from X to Y` on each transition, so a regression there is diagnosable from the console output. That is a deliberate and acceptable pattern. The other two gated on a single `Done` predicate and reported a regression as a bare timeout with nothing to read.

**Detect:** files with a `[Test]` and zero `Assert.` occurrences, minus those using the `Sequence` helper. Seven have no assertion, five of them legitimately. `ErrorImportPerformanceTests` is fixed: it records the count on the context, which the runner prints when a scenario does not finish. That leaves `When_single_message_fails_in_batch`, in the area phase 3 sweeps next.

### 3. Assertion restates the condition the scenario already waited on (unfalsifiable)

`When_a_invalid_id_is_sent_to_retry` ended with `Assert.That(context.Done, Is.True)` after `.Done(ctx => ctx.Done)`. That assertion could not fail: if the flag were false the scenario would have timed out first. The real subject of the test, that posting a retry for a non-existent id does not break subsequent batches, was never asserted, and the response of the invalid POST was never inspected.

**Detect:** a mechanical grep finds candidates of the shape `Assert.That(context.Flag, Is.True)`, currently 9. The confirmed one is fixed and now asserts the status the unknown id answers with. The rest need reading individually, because a flag set by a message handler and gated on something else is legitimate: `When_failed_message_searched_by_body_content` was read and is sound, since its `Done` returns true whether or not the flag is set. Four of the nine are in `ExternalIntegration`, which is parked.

### 4. Assertions coupled to one persister's internals (portability)

Tests that assert RavenDB implementation details rather than the contract both persisters offer. The multi-attempt tests asserted Raven's ten-attempt trimming and its full attempt history, neither of which EF provides by design. These read as EF gaps when they are really over-specified tests, and they are the main reason the EF exclusion list looks longer than the real feature gap.

**Detect:** the `<Compile Remove>` blocks in the two EF acceptance csprojs are the existing inventory. Each entry is either a real gap, a settled design difference, or an over-specified test, and the comments do not currently distinguish them.

### 5. Setup computed but never asserted (rot)

Fixtures, headers, constants and context properties that exist to support an assertion that was removed or never written. `Recoverability/MessageFailures` held eight: a `Retried` flag written by a handler and read by nothing in four tests whose subject is that a retry happened, two `FromAddress` and two `LocalAddress` captures, each with a constructor parameter that existed only to feed them. Thirteen `Console.WriteLine` calls in eleven files were the same thing in another form, printing either nothing identifying or what the neighbouring assertion message already says.

Harmless on its own, but it makes tests read as though they cover more than they do, which is how the first two patterns survive review.

**Detect:** per-file reading. Context properties with a setter and no read outside the scenario are the strongest signal.

## The routes that need tests

40 of the 85 routes were called by no acceptance test in any suite when this list was confirmed, route by route, against the two approved route lists and the test sources. The licensing block has since been covered, leaving 32. This is the whole list rather than a sample of it, and ticking every box here is what closes phase 4.

The groups are sized to be one PR each and ordered by what breaks in ServicePulse if the route regresses. Each entry names the ServicePulse consumer where there is one, because that is what the test should assert: the contract the UI relies on, not a 200.

### Nav-gating routes

If one of these regresses, ServicePulse loses a whole section of its navigation and nothing in the suite notices.

- [ ] `GET /api/heartbeats/stats`: gates the Heartbeats nav item (`viewHeartbeats`)
- [ ] `GET /api/messages2`: gates the audit messages nav item (`viewAuditMessages`)
- [ ] `GET /api/license`: gates the Licence nav item (`viewLicense`)

`GET /api/licensing/report/available` gates the Throughput nav item and belongs to this tier too, but it is written with the rest of the licensing block below. `GET /api/connection` gates the Connections nav item and is covered in MultiInstance only, so it is a home decision rather than a new test.

### Licensing and throughput (done)

All eight `api/licensing` routes, covered by one scenario: `ServiceControl.AcceptanceTests/Licensing/When_creating_a_usage_report_on_a_non_broker_transport`. The open question about whether this needed its own test project is answered: `LicensingComponent` is in `ServiceControlMainInstance.Components`, so the acceptance host already starts it, and both the Raven and EF persisters register an `ILicensingDataStore`, so the test needs no exclusions.

The scenario arranges throughput the way it really arrives, by dispatching the message a monitoring instance sends to the throughput queue, then walks the ServicePulse throughput page: check a report is possible, review where the numbers come from, correct the queue that is not an NServiceBus endpoint, redact the customer name, download the report, and assert the redaction and the correction both survive into the file that gets sent to Particular.

- [x] `GET /api/licensing/report/available`: gates the Throughput nav item (`viewThroughput`)
- [x] `POST /api/licensing/settings/masks/update`: `manageThroughput`
- [x] `GET /api/licensing/settings/masks`: throughput masking settings
- [x] `GET /api/licensing/settings/test`: connection test on the throughput settings page
- [x] `GET /api/licensing/settings/info`: throughput settings summary
- [x] `GET /api/licensing/report/file`: downloads the throughput report
- [x] `GET /api/licensing/endpoints`: endpoint throughput list
- [x] `POST /api/licensing/endpoints/update`: saves per-endpoint user indicators

Writing it surfaced a domain rule no route-level test would have reached: a report counts only complete days, so throughput recorded for today does not make one available. The first version of the scenario reported today's numbers and sat on a 90-second timeout. That rule is now stated in the test's arrangement.

Each route is covered on both of its branches, by one scenario each. The suite runs on LearningTransport, which registers no `IBrokerThroughputQuery`, so `ThroughputCollector` receives null for it and takes the branch only Learning and MSMQ take. RabbitMQ, Azure Service Bus, SQS, SQL Server and PostgreSQL all register one, and on that branch `report/available` requires broker-sourced throughput rather than any throughput, `settings/test` runs a broker connection test, `settings/info` returns the broker's settings, and the report carries a `ReportMethod` of `Broker` rather than `ServiceControl`. The test names say which branch each one covers.

- [x] Cover the broker path for the eight `api/licensing` routes

`When_creating_a_usage_report_on_a_broker_transport` covers the branch every production install except MSMQ takes. It registers a fake `IBrokerThroughputQuery` through `CustomizeHostBuilderBeforeServiceControl`, because `AddLicensingComponent` decides whether to start `BrokerThroughputCollectorHostedService` by checking whether a query is registered, and it decides that while `AddServiceControl` runs. It also replaces the collector registration with one whose `DelayStart` is zero, since the production value of 40 seconds is longer than a scenario should take.

Beyond the branch itself it asserts the grouping: a broker queue and a monitored endpoint whose names differ only by the sanitized character have to end up as one endpoint in the report, or a customer's usage is counted twice. Making the fake's `SanitizeEndpointName` an identity function fails that assertion, so it is doing real work.

Getting the two scenarios to pass together turned up a fault in the suite that had nothing to do with them. `RavenPersisterSettings.ThroughputDatabaseName` defaults to a fixed name, and the Raven acceptance storage configuration set only `DatabaseName`, so throughput and licensing data from every acceptance test shared one database while everything else was isolated per test. The persistence tests already set it per test; the acceptance ones now do the same, and clean it up. Two things made this hard to see: the symptom looked like queue interference, because the two tests' endpoints differ only by the character the broker sanitizes and so merged into one grouped endpoint, and the `MonitoringService` bug fixed separately was losing endpoints at the same time. The EF suites were never affected, since throughput lives in their per-test database.

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
- [ ] `GET /api/endpoints/{name}/errors`: failed messages filtered to one endpoint

### Retry and resolve routes

- [ ] `POST /api/errors/queues/{queueAddress}/retry`: retry everything for a queue
- [ ] `PATCH /api/pendingretries/queues/resolve`: resolve pending retries by queue
- [ ] `PATCH /api/errors/{from}...{to}/unarchive`: unarchive by date range

### Audit-backed message queries

These need an audit instance, so decide whether MultiInstance is the right home before adding them to the primary suite.

- [ ] `GET /api/messages/search`: the `?q=` form. The `/search/{keyword}` form is covered
- [ ] `GET /api/endpoints/{endpoint}/messages/search`: the same, scoped to an endpoint
- [ ] `GET /api/endpoints/{endpoint}/audit-count`: audit counts per endpoint

### Configuration surface

- [ ] `GET /api/configuration`: only `/api/configuration/remotes` is covered today
- [ ] `GET /api/instance-info`: same action as `/api/configuration`, separate route
- [ ] `GET /api/edit/config`: the edit-and-retry feature flag ServicePulse reads before offering edit
- [ ] `GET /api/license/details`
- [ ] `POST /api/license/detailsUpload`

### Deliberately untested

No ServicePulse journey reaches these, and ServicePulse issues no HEAD request at all, so no scenario in this plan will cover them. Recorded here so that they stay a decision rather than an oversight: if a consumer for them turns up, they need tests.

- `HEAD /api/errors`: the count behind failed-message paging
- `HEAD /api/recoverability/groups/{id}/errors`: the count behind group paging

`HEAD /api/redirect` is the exception and is already covered, by `When_a_request_is_repeated_with_its_etag`.

### Covered only in MultiInstance

Not gaps, but not in the primary suite either. Decide deliberately whether MultiInstance is the right home before duplicating any of them.

- [ ] `GET /api/connection`: gates the Connections nav item (`viewConnections`)
- [ ] `GET /api/endpoints/known`: known endpoints list, covered by `When_endpoint_known_to_audit_instance`
- [ ] `GET /api/conversations/{id}`: sequence diagram
- [ ] `GET /api/sagas/{id}`: saga diagram
- [ ] `GET /api/endpoints/{endpoint}/messages`
- [ ] `GET /api/endpoints/{endpoint}/messages/search/{keyword}`

Worth being precise about the heartbeats entry, because several others are the same shape. Heartbeat *ingestion* is well covered: six tests drive endpoints starting up, going quiet, and being marked monitored. What no test calls is `/api/heartbeats/stats`, the endpoint ServicePulse's heartbeats page actually reads. The plumbing is tested; the contract on top of it is not.

## The work

### Phase 1: confirm the gap list properly (done)

The original table was grep-based, and a route reached through a helper, a constant or an interpolated base path would have been missed. The list above replaces it, built from both approved route lists rather than from greps and checked route by route against the primary and MultiInstance sources. Reading only the ServiceControl route list is what hid the licensing routes in the first place, so both were read.

Two details mattered while confirming it. The approved lists carry the action-level template only, so they hold two rows both reading `GET /configuration`, one on `RootController` under `api` and one on `AuthenticationController` under `api/authentication`; the controller-level `[Route]` has to be folded in or the two merge. And the verb has to be tracked separately from the path, because ServicePulse gates `GET` and `POST` on `/api/notifications/email` as two different capabilities.

Six entries changed as a result. `GET /api/endpoints/known` is not a gap: MultiInstance covers it. The other five were gaps the grep missed, and four of them gate a ServicePulse capability: `GET /api/license` and `GET /api/connection` gate nav items, and `DELETE /api/customchecks/{id}`, `PATCH/POST /api/errors/archive` and `POST /api/recoverability/groups/{id}/errors/unarchive` are actions the UI offers.

No tooling came out of this phase, deliberately. A test that scans the suite's source to police its own coverage is a second thing to maintain and gets stale in its own way; the list above is the artefact, and new routes are a review-time concern.

### Phase 2: the silent-registration class (done)

The whole suite registers services from a test in seven places, so the audit was exhaustive rather than a sample. Five were correct. Two were the known `IEnrichImportedErrorMessages` cases, already fixed. One was new:

`When_a_critical_error_is_triggered` registered `CriticalErrorCustomCheck` as its own concrete type, with a comment saying it overrode the production registration to shorten the check interval. It did not. The check is registered with `TryAddEnumerable` against `ICustomCheck` and consumed through `GetServices<ICustomCheck>()`, so the test's registration was never resolved and the check ran on its 60-second production interval. The test passed either way, about a minute slower than intended. It now removes the production registration explicitly and re-adds the check against `ICustomCheck`, and runs in six seconds.

One registration is worth knowing about even though it is correct. `When_a_retry_fails_to_be_sent` substitutes a `FakeReturnToSender` by re-registering `ReturnToSender`, which works only because `CustomizeHostBuilder` runs after all production registration and `ReturnToSenderDequeuer` resolves a single instance rather than a collection. That is a real distinction, not a detail: the same move against a collection adds a second implementation and leaves the production one running.

No harness check came out of this phase. The underlying fault is a test written so that it could pass without its own setup taking effect, and a convention test policing registrations would catch one shape of that while leaving the rest. The practice is written down instead, in [Writing acceptance tests](writing-acceptance-tests.md), which covers registering against the injected abstraction, replacing a production registration so that it fails loudly if production moves, and asserting on evidence the double actually ran.

### Phase 3: sweep the 71 files, one area per PR

Read for the five patterns above, area by area, so each PR stays reviewable and the diff maps to one owner's mental model. Fix what is cheap to fix in the same PR; raise anything that changes what a test means as its own change with the reasoning written down.

- [x] `Recoverability/MessageFailures`, 22 files, the densest area and the one the EF work touches most.

  One unfalsifiable assertion, `When_a_invalid_id_is_sent_to_retry`, which asserted the flag its own `Done` predicate had already waited for. It now asserts what the test is named for: retrying an id that does not exist answers `202 Accepted` rather than rejecting, and the scenario still completes, so the batch behind it kept moving. The retry loop it used to sit behind is gone. It was there to wait for an API that is never not ready: the instance is started while the component runner is created, before any endpoint starts, and ServiceControl has no code path that answers 503. The loop also caught every non-success alike, so a genuine rejection would have spun rather than failed.

  One test reporting failure as a bare timeout, `ErrorImportPerformanceTests`. The count now goes on the scenario context, which the runner prints when a scenario does not finish, so a failure says how many of the 100 messages arrived.

  Dead setup in six files: `Retried` in four, `FromAddress` in two, `LocalAddress` in two, each written by a handler and read by nothing, along with the `ReceiveAddresses` parameter that only existed to feed them. `Retried` was the misleading one, sitting in tests whose subject is that a retry happened while `RetryCount` did the actual work.

  Nothing found for the persister-coupling pattern: no file in this area is excluded from the EF suites. The two registrations here were already settled in phase 2.

  Three tests moved to the `Do` sequence helper, which is where the area's readability was worst. `When_a_retry_for_a_failed_message_is_successful` held a four-step sequence inside a single `Done` predicate five times over, re-entered on every poll, with a `RetryIssued` guard to stop the retry firing repeatedly. As steps that guard is unnecessary, though the flag itself stays because the handler reads it to decide whether to throw. `When_a_failed_message_is_pending_retry` and `When_a_invalid_id_is_sent_to_retry` had the same shape spread across chained endpoint `When` clauses. A stalled run now names the step it stopped on rather than only the elapsed time.

  Thirteen `Console.WriteLine` calls went, across eleven files. Six were a bare "Message Handled" in a handler, which carries no identity and fires on every delivery, so in tests turning on how many times a message was handled it cannot tell the first attempt from the retry. Worse, they sat next to the counter that does answer that, and the runner already prints the context on failure. The rest either narrated a step that throws with detail when it fails, or dumped state next to an assertion whose message says the same thing. They read as debugging left in place rather than diagnostics anyone chose.

  Not every test wants this. A test that sends a message, waits for one thing and asserts reads worse as a sequence, which is most of `When_a_message_has_failed`. A step also has no `bus`, so anything that sends has to stay an endpoint `When`.
- [ ] `Recoverability/*` root, `Groups`, `MessageRedirects`: the retry, group and redirect flows.
- [ ] `Monitoring/*` and `EventLogs`: heartbeats, custom checks, endpoint monitoring.
- [ ] `Recoverability/ExternalIntegration` and `Monitoring/ExternalIntegration`: hold until the EF external-integration work lands, then review against both persisters at once.

### Phase 4: work through the route list

Straight down the list in "The routes that need tests", one PR per group, in the order the groups are given. The phase is done when every box is ticked.

Each test asserts the contract its consumer relies on rather than just a 200: the shape ServicePulse reads, the status it branches on, the effect the action has on the next request. A test that only proves the route is routable would leave the same gap in a different form.

## What this plan does not do

It does not keep the route list current by machine. The list is a snapshot taken during phase 1, and a route added after that will not appear in it on its own. Catching those is a review-time concern: a new controller action arrives with the PR that adds it, which is where the test for it belongs.

It does not review the `Security/*` tests, the audit instance, or the monitoring instance.

It does not treat persister parity as a workstream in its own right. It appears only as one defect pattern, on the grounds that most of the current EF exclusions are over-specified tests rather than missing features, which is itself a claim worth confirming during phase 3.
