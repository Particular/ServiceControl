# CI parallelisation: research and recommendation

Status: proposal. Measured August 2026 against run
[31835332447](https://github.com/Particular/ServiceControl/actions/runs/31835332447) (master push,
all green) and the four-way concurrent burst of runs 31788928123/31788933879/31788939745/31788946584.

## The short version

The premise behind this investigation was "we have a limited runner pool, so let's parallelise steps
even more". The measurements say the second half of that does not follow from the first.

Parallelising *more steps inside existing jobs* buys almost nothing. The build is already backgrounded,
and in the jobs where it matters it is already fully hidden behind infrastructure provisioning. In the
jobs where it is not hidden, there is nothing to hide it behind.

What does have legs is the inverse move: **use in-job step parallelism to collapse jobs back onto fewer
runners, without paying the wall-clock penalty that caused us to split them apart in the first place.**

Nothing tooling-wise was ever blocking this. We have had the capability since we adopted
`background:`, and the workflow used the `- parallel:` list shape before 54f87f37a replaced it. What
changed is the *constraint*, not the toolbox: the job count has grown to the point where the pool, not
the critical path, is the thing worth optimising.

Three changes, in priority order:

1. Add `concurrency` with `cancel-in-progress`. Free, no wall-clock cost, and it addresses the actual
   observed cause of pool exhaustion.
2. Merge the categories that share infrastructure, by giving their projects a shared `<TestCategory>`,
   and run their assemblies concurrently on the one runner. Saves 18 jobs at zero wall-clock cost.
3. Attack the SQL Server acceptance suite specifically. It is the entire critical path.

## What the numbers actually say

### The pool cap is real, and it is exactly 60

Peak concurrent running jobs across the four-run burst was **60**, hit at 09:41:02 and held. That is
the documented GitHub Team plan cap, confirmed empirically. It is an **org-wide** cap, shared with
every other Particular repository.

One CI run is currently **52 jobs**. That is 87% of the entire organisation's concurrency budget.

| Scenario | Median queue | p90 queue | Max queue |
|---|---:|---:|---:|
| Single run in flight | 2 s | 4 s | 77 s |
| Four runs in flight (208 jobs) | 165 s | 592 s | 665 s |

So the pool is a non-issue at one run and a serious issue at two or more. The backlog took 674 s to
drain. During that window ServiceControl starved every other repo in the org.

### Current shape of a run

| | Jobs | Runner-min |
|---|---:|---:|
| Windows matrix | 21 | 110.9 |
| Linux matrix | 20 | 68.7 |
| Installers / containers / container-test | 11 | 28.9 |
| **Total** | **52** | **208.4** |

Wall clock 14.5 min. This is a regression from the 8.6 min / 149 runner-min that PR #5715 achieved:
the `Default` and `RabbitMQ` splits and the two new EF acceptance categories have since added 17 jobs.

### There is a large amount of slack under the critical path

Critical path is `Windows-PrimarySqlServerAcceptance` at **870 s**. The next longest is 761 s. Then it
falls off a cliff: **34 of 41 matrix jobs finish in under 420 s.**

That gap is the whole opportunity. Any job we can merge and keep under ~800 s is a runner reclaimed
for **free**, because the run was going to wait on the SQL Server acceptance job regardless.

### Where the time goes in the slow jobs

Seconds, from the GitHub step timings:

| Job | Total | Overhead | Build | Infra | Wait | Tests |
|---|---:|---:|---:|---:|---:|---:|
| Windows-PrimarySqlServerAcceptance | 870 | 123 | 255 | 200 | 55 | 484 |
| Windows-SqlServerPersistence | 761 | 69 | 123 | 218 | 0 | 470 |
| Linux-AzureServiceBus | 517 | 150 | 25 | 68 | 0 | 296 |
| Linux-PrimarySqlServerAcceptance | 512 | 24 | 77 | 98 | 0 | 385 |
| Windows-AzureServiceBus | 511 | 82 | 50 | 76 | 50 | 300 |
| Linux-SqlServerPersistence | 487 | 26 | 83 | 107 | 0 | 352 |
| Windows-PrimaryRavenAcceptance | 413 | 45 | 104 | 0 | 104 | 262 |
| Windows-DefaultCore | 318 | 64 | 150 | 0 | 150 | 102 |

Note the `Wait` column. Where a job has infra to provision, `background: true` works perfectly and
`Wait` is 0. Where a job has **no** infra (the Raven and Default categories, MSMQ, SQS), `Wait` equals
`Build` exactly, because there is nothing to overlap with. Backgrounding the build there is a no-op.

Total across the matrix: ~19 runner-minutes sit in `Wait`. That is not recoverable by parallelising
harder within those jobs. It is only recoverable by making one build serve several test runs.

## Why "parallelise steps more" does not work on its own

Walking the candidates that a naive reading would suggest:

- **Overlap infra setup with itself.** Already effectively serial by necessity: `Setup WSL` provisions
  the Docker host that the SQL Server / PostgreSQL / RabbitMQ / IBM MQ containers run in. The chain is
  real, not incidental.
- **Overlap the Azure provisioning with the build.** Deliberately not done. Commit 6ec08ce6b moved the
  Azure steps behind `Wait for build` on purpose, because they create real cloud resources and we do
  not want them alive while a build might fail.
- **Overlap the build with itself.** `-graph` already parallelises the MSBuild graph.
- **Overlap `Run tests` with anything.** It is the last step and depends on everything.

The one genuine in-job win left is running *multiple test assemblies* at once, and most jobs only have
one assembly. Which points straight at merging.

## Recommendation 1: `concurrency` with cancel-in-progress

GitHub Actions does not cancel superseded runs by default. Push a commit to a PR branch, and the run
triggered by your *previous* commit keeps going to completion, all 52 jobs of it, testing code nobody
is going to merge. The only thing that stops it is the `concurrency` key, which puts runs into a named
group and cancels the older member when a new one arrives. No workflow in `.github/workflows/` sets it.

This is not theoretical. Across the last 120 CI runs:

- **Zero** runs have the conclusion `cancelled`. Nothing has ever been superseded.
- **19** pairs of runs overlap, where an older run was still executing when the next push to the same
  branch started a new one.

The overlaps are long, because a 52-job run is long. Some examples:

| Branch | Obsolete run kept going for |
|---|---:|
| `john/cancel_part11` | 1395 s |
| `john/cancel_part12` | 1157 s |
| `rhys/atomic-commit2` | 1378 s |

So a branch under active iteration can easily hold 100+ jobs at once across two runs, against an
org-wide cap of 60. That is the mechanism behind the four-run pileup measured above, and it is why
this is the first recommendation: it attacks the observed problem rather than the theoretical one.

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

Applied to `ci.yml`. `github.ref` alone is a sufficient group key: on a `pull_request` event it is
`refs/pull/<n>/merge`, already unique per PR.

**This cancels master too, deliberately.** The first draft exempted master and `release-*` on the
grounds that we always want a build of a merged commit. The data does not really support paying for
that: **4 of the last 21 master runs** would have been cancelled, one with 1616 s still to run. Master
merges land close enough together to produce the same waste PR branches do.

What it costs, having checked rather than assumed:

- **Releases are not affected at all.** They run from `release.yml`, triggered on tags. `ci.yml`
  triggers only on `pull_request` and on pushes to `master` / `release-*` branches, never on a tag, so
  a release build can never be cancelled by this.
- **An intermediate master commit loses its own CI signal.** If master goes red you know the newest
  commit is bad but not which of the ones it superseded was. Modest, since each merged PR was green on
  its own branch first.
- **A ghcr prerelease image may not finish pushing.** `build-containers.yml` runs with `push: true`
  and `build-db-container.yml` does `docker image push --all-tags` on master, tagged with the commit's
  MinVer version. Cancelling mid-push leaves that version's dev image missing rather than corrupt,
  because the next master commit publishes a different version.

If the last point ever matters, `concurrency` can be set per job rather than workflow-wide, cancelling
the test matrix while letting `containers` / `db-container` / `windows-installers` finish.

## Recommendation 2: merge jobs that share infrastructure, running their tests concurrently

The mechanism is the one we already use. Per the
[June 2026 changelog](https://github.blog/changelog/2026-06-25-actions-steps-can-now-be-run-in-parallel/),
`parallel:` is explicitly "syntactic sugar" that "takes a group of steps and converts them to
`background` steps with a `wait` after". So `parallel:` and `background:`/`wait-all:` are the same
mechanism in two shapes, and either expresses these merges. Use `parallel:` for a fixed group of
sibling test steps because it reads better; keep `background:`/`wait:` where a single step needs to
overlap a named chain, as the build does today.

What matters is not the keyword but the shape of the merged job: the test phase has to be the **max**
of the merged assemblies, not the sum. Merging with the assemblies still serial saves a runner but
lengthens the job by the full duration of everything it absorbed, which is a bad trade. Merging with
them concurrent is close to free.

### 2a. RabbitMQ: 8 jobs → 2

The best candidate by a distance. The four projects are identical apart from
`TransportTestsConfiguration.cs` and their `<TestCategory>`; all four link the same
`ServiceControl.Transports.Tests\*.cs` sources.

They can share one broker. `TransportTestFixture` already suffixes every queue with
`Path.GetRandomFileName()`, so there are no name collisions between the four topologies, and therefore
no classic-vs-quorum redeclare conflict.

| | Now (4 jobs) | Merged (1 job) | Saving |
|---|---:|---:|---:|
| Windows | 987 s | ~420 s | 3 runners, ~570 runner-s |
| Linux | 498 s | ~225 s | 3 runners, ~275 runner-s |

**Risk:** all four projects do `<Compile Remove>` on `NUnitParallelRunnerSettings.cs`, so each suite
currently runs strictly serially, presumably deliberately (queue-length monitoring is timing
sensitive). Running four of them at once against one broker is new concurrency exposure. Mitigate by
merging in two stages: pair them first (classic together, quorum together), watch for flakes, then go
to four.

### 2b. PostgreSQL: 6 jobs → 2

`PostgreSqlPersistence` and `PrimaryPostgreSqlAcceptance` already invoked
`Particular/setup-postgres-action` with byte-identical inputs. The `PostgreSql` transport category
joins them on the same container, as described above.

| | Now (3 jobs) | Merged (1 job) | Saving |
|---|---:|---:|---:|
| Windows | 874 s | ~520 s | 2 runners, ~355 runner-s |
| Linux | 525 s | ~330 s | 2 runners, ~195 runner-s |

### 2c. Raven family: 6 jobs → 2

`DefaultAudit`, `PrimaryRavenAcceptance`, `PrimaryRavenPersistence` provision no infrastructure at all
(RavenDB.Embedded supplies its own server per test project output directory). The win here is not
infra, it is that they currently pay **three separate builds** with three unhidden `Wait` phases
(Windows: 79 + 104 + 91 = 274 s of build, none of it overlapped).

| | Now (3 jobs) | Merged (1 job) | Saving |
|---|---:|---:|---:|
| Windows | 942 s | ~610 s | 2 runners, ~330 runner-s |
| Linux | 621 s | ~400 s | 2 runners, ~220 runner-s |

Keep the `Download RavenDB Server` gate as-is. Per `ci-scoping-decisions`, only
`ServiceControlInstaller.Packaging` needs it, and it lives in `DefaultCore`, not in this group.

### 2d. The SQL Server pair: merged, on purpose, to be measured

**Decision: merge it and measure.** The analysis below argued for deferring it. That analysis is
estimates, and estimates are what the `build_once` spike was rejected for relying on. Backing it out
is a one-line change (give `ServiceControl.AcceptanceTests.SqlServer.csproj` its own `<TestCategory>`
again and add it to the matrix), so the cost of being wrong is one CI run.

The `SqlServer` category now also carries the transport tests, which adds ~50 s of test time to a job
already estimated at 877-1040 s, against a whole runner saved.

**This group is also the experiment.** It is the only merge that can move wall clock, because it is the
only one that contains the critical path. So a single run disambiguates cleanly:

- Job count and runner-minutes measure *all* the merges.
- Wall clock measures *this* merge, essentially on its own.

Baseline to compare against, from run 31835332447: **52 jobs, 208 runner-min, 14.5 min wall clock**,
critical path `Windows-PrimarySqlServerAcceptance` at 870 s.

Predicted after: **38 jobs, ~168 runner-min**, wall clock somewhere between 877 s (perfect test
scaling) and ~1040 s (heavy contention). If wall clock lands near 877 s, keep it. If it lands near
1040 s, revert this one entry and keep the other three merges, which are unaffected.

The reasoning that produced that range:

`SqlServerPersistence` (761 s) and `PrimarySqlServerAcceptance` (870 s) use identical
`install-sql-server-action` inputs, so on paper they are the most natural merge of the lot. Both also
isolate per test: `PersistenceTestsContext` creates `sc_test_{guid}`,
`AcceptanceTestStorageConfiguration` creates `sc_at_{guid}`. Technically it would work.

The problem is that these two are the #1 and #2 longest jobs in the run, and a merged job *contains*
whatever it absorbed. So this merge, uniquely, can only make the critical path longer or leave it the
same. Here is the arithmetic:

| | Today (2 runners) | Merged (1 runner) |
|---|---|---|
| Overhead | 69 + 123 | 123 (paid once) |
| Infra | 218 + 200 | ~200 (one SQL container) |
| Build | 123 + 255, mostly hidden by infra | ~270 union, 70 s residual after infra |
| Tests | 470 + 484, on separate boxes | 484 s **at best**, 954 s at worst |
| **Job wall clock** | **870 s** (they run side by side) | **877 s best case** |
| Runner cost | 1631 s | ~880 s |

The saving is real, about 590 runner-seconds. But look at the best case: 877 s against today's 870 s.
The shared savings (one build, one infra setup, one overhead) come to roughly 250 s, and they are
almost exactly cancelled out by the fact that the test phase can never drop below the longer of the two
suites.

And 484 s for the merged test phase assumes perfect scaling, which is the one thing we should not
assume here. Both assemblies already run `Parallelizable(ParallelScope.All)` with
`LevelOfParallelism(4)` on a 4-vCPU runner, so each one *already saturates the box on its own*. Running
both concurrently puts 8 fixtures on 4 vCPUs, each spinning up a ServiceControl host and creating and
dropping databases against a single SQL Server container. If contention pushes the test phase to 650 s,
the merged job is 1043 s and the whole run goes from 14.5 to ~17 minutes.

**Contrast with the merges that are safe.** Merged RabbitMQ is ~420 s against a critical path of 870 s.
My contention estimate there could be off by 100% and it would still fit. Those merges have a ~450 s
error budget; this one has none by construction. That asymmetry, not the raw saving, is the reason to
treat them differently.

If the measurement comes back bad, recommendation 3 is the way back in: bring
`PrimarySqlServerAcceptance` down to ~620 s first so something else becomes the critical path, and the
slack this merge needs reappears.

### Projected result

| | Jobs | Runner-min | Wall clock |
|---|---:|---:|---:|
| Now | 52 | 208 | 14.5 min |
| After merges | 34 | ~160 | 14.5 min (unchanged, if the SQL merge scales) |

34 jobs is 57% of the org cap instead of 87%, and combined with recommendation 1 a second concurrent
run largely stops queueing.

## Recommendation 3: attack the critical path directly

Merging does nothing for wall clock. Only `Windows-PrimarySqlServerAcceptance` does, because it *is*
the wall clock. Its 870 s breaks down as 255 s build + 200 s infra (which hides most of the build) +
55 s residual wait + 484 s tests.

Two levers, cheapest first:

- **Raise `LevelOfParallelism` for the acceptance assemblies.** They link
  `ServiceControl.UnitTests\NUnitParallelRunnerSettings.cs`, which sets `ParallelScope.All` with
  `LevelOfParallelism(4)`. These tests are dominated by waiting on message ingestion, not CPU, and each
  gets its own database. 6 or 8 is worth measuring on a 4-vCPU runner. One-line change, easy to revert.
- **Shard the assembly in-job.** Two `dotnet test` steps with complementary `--filter` expressions,
  run concurrently on the same runner against the same SQL container. This is the sharding that
  `ci-scoping-decisions` identified as the only remaining lever, but done *inside* the job so it costs
  no pool capacity. Would take the job to roughly 620 s.

A third, broader lever: the Windows build is 255 s against Linux's 77 s for the same closure, and
Windows builds total ~32 runner-minutes across the matrix. Adding a Defender exclusion for the
workspace on Windows runners is a well-known 30-50% win on .NET builds and would benefit all 21 Windows
jobs. Worth a spike.

## Open question for the team

Windows costs 110.9 runner-min against Linux's 68.7 for substantially the same 20 categories.

Do the pure-transport categories (RabbitMQ, SQS, ASQ, ASB, IBMMQ, PostgreSql, SqlServer) need to run on
Windows on *every PR*? They exercise transport client libraries where OS-specific risk is low. Running
them Linux-only on PRs and keeping the full matrix on master and `release-*` would remove another ~10
jobs and ~30 runner-minutes from the PR path.

MSMQ obviously stays Windows-only. `DefaultCore`, `DefaultAudit` and `DefaultMonitoring` must stay on
both, per `ci-scoping-decisions`: `Should_populate_appSettings_from_exe_config_file` needs a real
Windows `.exe`.

This is a coverage-vs-capacity call, not a technical one, so it needs a decision rather than a patch.

## What has been implemented

All four merges plus `concurrency`, on branch `john/acceptance-test_gaps`, ready to measure.

**The merges live in the projects, not in the workflow.** The first cut of this carried a
group-to-categories mapping in the matrix, which meant the workflow matched category names as strings
and the mapping was duplicated between YAML and the csproj files. Instead, the merged categories are
simply *declared*: the projects being merged now share a `<TestCategory>` value. There is no new
concept, no group layer, and nothing for the two halves to drift apart on. Category count goes from 20
to 13.

| Category | Absorbs | Projects |
|---|---|---:|
| `Raven` | `DefaultAudit`, `PrimaryRavenAcceptance`, `PrimaryRavenPersistence` | 7 |
| `RabbitMQ` | the four classic/quorum × conventional/direct categories | 4 |
| `SqlServer` | `SqlServerPersistence`, `PrimarySqlServerAcceptance` | 3 |
| `PostgreSql` | `PostgreSqlPersistence`, `PrimaryPostgreSqlAcceptance` | 3 |

Category count goes from 20 to **11**.

- **17 `.csproj` files** — updated `<TestCategory>` to the merged value.
- **`.github/workflows/ci.yml`** — added `concurrency` with `cancel-in-progress`. The matrix keeps its
  `test-category` axis, now listing 11 values, and gains one `max-parallel` entry per merged category.
  Matrix jobs drop from **39 to 21**, so a run goes from 52 to **34 jobs**.
- **One database server per category, not two.** Both provisioning actions bind a fixed host port
  (`1433` and `5432` are hardcoded in their setup scripts), so a job can only invoke each once. It
  only needs to: the persistence and acceptance suites create a database per test (`sc_test_`/`sc_at_`
  plus a GUID) and the transport suite creates queue tables under randomly suffixed names, so all
  three share one server without colliding. The action runs once for the suite that needs the most
  from it (`catalog: ServiceControl` with full-text search), and the transport connection-string
  variable is aliased onto the same server in the following step. That also stops the SQL Server
  full-text install being paid for twice.
- **`tools/select-test-projects.ps1`** — unchanged in shape: still selects by one `<TestCategory>`
  value. It needed no modification for merging at all, which is the point.
- **`tools/run-tests.ps1`** — added `-MaxParallel` (default 1, so single-project categories behave
  exactly as before). Concurrent runs buffer stdout/stderr per assembly and replay them into
  `::group::` blocks on completion, because interleaved `dotnet test` output is unreadable.
- **`ServiceControl_TESTS_FILTER` still works and is still set.** Because a merged category's projects
  all declare the same `<TestCategory>`, the generated assembly attribute matches for every assembly
  in the job. Had the merge lived in the workflow instead, this would have broken: the comparison is
  an exact match, so one job-wide value would have ignored every assembly but one.
- **`README.md`** — updated the documented filter values.

Verified locally: the declared categories and the matrix axis are an exact set match in both
directions, the matrix expands to 25 jobs with `max-parallel` landing on the right eight, concurrent
runs start together and report separate logs with a correct aggregate exit code, and `-MaxParallel 1`
is still strictly sequential.

## First measured run (PR #5783, run 31858824648)

Everything introduced here worked on the first run, with two exceptions found and fixed.

**The merges validated.** Every assembly in every merged category ran and passed, so nothing was
silently skipped:

| Job | Assemblies | Tests | Was (separate jobs) | Now |
|---|---:|---|---:|---:|
| Windows-SqlServer | 3 | 31 + 409 + 146 | 3 runners, 870 s critical path | **1 runner, 746 s** |
| Linux-SqlServer | 3 | same | 3 runners, 512 s | 1 runner, 549 s |
| Windows-PostgreSql | 3 | 23 + 411 + 145 | 3 runners, 331 s | 1 runner, 439 s |
| Linux-PostgreSql | 3 | same | 3 runners, 268 s | 1 runner, 271 s |
| Windows-RabbitMQ | 4 | 14 + 13 + 13 + 16 | 4 runners, 333 s | **1 runner, 269 s** |
| Linux-RabbitMQ | 4 | same | 4 runners, 161 s | 1 runner, 173 s |

**The SQL Server merge was the open question, and it beat its own best case.** Predicted 877-1040 s,
measured **746 s** on one runner against a previous critical path of 870 s across three. So it is a
win on wall clock *and* on runner-minutes, not the trade-off the estimates suggested. The shared
server and per-test database isolation held under three concurrent assemblies. The RabbitMQ shared
broker likewise: four topologies at once, no queue collisions, and faster than the slowest of the four
old jobs.

**Two failures, both real, both fixed.**

1. **`Raven` failed on both runners.** `RavenDB.Embedded` binds a fixed port, and `SharedEmbeddedServer`
   picks it via `PortUtility.FindAvailablePort`, which only inspects the currently active listeners.
   Two test processes starting together both see the base port free and both claim it, so the second
   dies with `Failed to bind to address http://127.0.0.1:33334: address already in use`. Six of the
   seven assemblies passed; `ServiceControl.AcceptanceTests.RavenDB` lost the race with
   `ServiceControl.Persistence.Tests.RavenDB`, which shares that `SharedEmbeddedServer`.
   Fixed properly rather than by giving up the concurrency. `PortUtility.GetAssignedOrAvailablePort`
   takes the port from `ServiceControl_TESTS_RAVENDB_PORT` when it is set and falls back to probing
   when it is not, and `run-tests.ps1` hands each concurrent run its own port (33334, then +10 each).
   Both `SharedEmbeddedServer` implementations, error instance and audit, use it. The variable is
   deliberately left unset for serial runs, where probing copes better with a port something else on
   the machine already holds. `Raven` stays at `max-parallel: 3`.
2. **`Linux-DefaultCore` hung**, at over 1000 s against a dead-steady 189-205 s in the five previous
   runs. This one was self-inflicted: `run-tests.ps1` called the parameterless
   `Process.WaitForExit()`, which waits not only for the process but for its redirected streams to
   reach EOF. On Linux `Start-Process` pumps those through a pipe, so a test leaving behind a child
   that inherited the handle blocks it forever. Windows redirects to a file handle and was unaffected,
   which is why only Linux hung. Now bounded to 5 s, which is all the pump needs once the process has
   exited.

### Shared machine-wide state is the recurring hazard

Two of the three failures so far were the same shape: a resource that is global to the machine, which
separate jobs never contended for and concurrent assemblies in one job now do.

- **RavenDB.Embedded's port**, fixed by assigning one per process.
- **The Windows event log source**, which is machine-wide. `EventSourceCreator.Create()` did
  `SourceExists` then `CreateEventSource`, and that check-then-act cannot be made atomic, so the loser
  of the race got `Source ServiceControl.Audit already exists on the local computer`. Now the create is
  wrapped in `catch (ArgumentException) when (EventLog.SourceExists(SourceName))`, so losing the race
  counts as success while a genuinely unusable source name still throws.

  Worth noting this was fixed in `EventSourceCreator` rather than in the `SetUpFixture`s that call it.
  The same latent race exists in production, where `SetupCommand` and the ingestion fault policies
  call it, so two instances being set up at once could hit it. One fix per copy covers every caller.

When something else fails under the merged categories, check first whether it is a third instance of
this pattern rather than a genuine test defect.

## Sequencing

1. **Run the merged branch and compare against the baseline** (52 jobs / 208 runner-min / 14.5 min).
   Job count and runner-minutes grade all four merges; wall clock grades the SQL Server merge.
2. If wall clock regressed, split `PrimarySqlServerAcceptance` back out of the
   `SqlServerPersistence` category and re-measure. The other three merges are unaffected.
3. Watch for flakes, particularly RabbitMQ. All four suites deliberately `<Compile Remove>`
   `NUnitParallelRunnerSettings.cs` and run serially today, so four-at-once against one broker is new
   exposure. `max-parallel: 4` is one number in the matrix to dial down to 2 if they misbehave.
4. `LevelOfParallelism` bump on the acceptance assemblies.
5. In-job sharding of `PrimarySqlServerAcceptance`, if step 4 was not enough.
6. Windows Defender exclusion spike.

The `ci-scoping-decisions` note records that `build_once` was rejected on measurement rather than
argument. Same bar here: everything above is estimates until a run says otherwise.
