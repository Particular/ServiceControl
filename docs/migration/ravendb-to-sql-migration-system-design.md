# How the migration is put together

*Written for someone about to change the migration code, not for someone running a migration. [The overview](ravendb-to-sql-migration-overview.md) says what the migration does and what a customer sees. This page says which class does it, who calls it, and why it sits where it does, so anyone moving one piece can see what else moves with it. Read it before you reorder the startup checks or add a category, because in both of those the order is the design. The operator steps are in [the instructions](ravendb-to-sql-migration-instructions.md).*

Setting keys on this page are written as the code writes them, relative to the `ServiceControl` settings root. An operator sets `ServiceControl/Migration/Enabled`, which the code calls `Migration/Enabled`.

## Everything at once, before the detail

![The migration's contracts, the engine between them, and the two persisters on either side. Purple bars mark the parts that are designed but have no call path in this build.](migration-system-design-diagram.png)

## Four assemblies, and what each one is allowed to know

| Assembly | What it holds | Why there |
| --- | --- | --- |
| `ServiceControl.Persistence` | `MigrationEngine` and the contracts it drives: `IMigrationSource`, `IMigrationTarget`, `IMigrationCheckpointStore`, `IMigrationStartupCheck`, `IMigrationTargetReadiness`, `IMigrationState`, `IMigrationSourceFactory`, plus `MigrationCategoryIds`, `MigrationCategoryRegistry`, `MigrationCheckpoint`, `MigrationBatch`, `MigrationWriteResult`, `MigrationSourceDescription`, `MigrationSkipReason`, `MigrationCategoryStateExtensions`, `CheckpointMigrationState`, `MigrationCheckpointConflictException`, `MigrationEngineOptions`, `MigrationSettings` and `HaltThreshold` | Both persisters and the host reference it, and it references neither persister. That is what lets the engine be driven entirely by fakes in `ServiceControl.UnitTests/Migration` |
| `ServiceControl.Persistence.RavenDB` | `RavenMigrationSource`, `RavenReadOnlySourceLifecycle`, `RavenDocumentStream`, one `IMigrationCategoryReader` per category, `SourceDataVersionIsReadableCheck`, `RavenDataVersion` | Everything that knows a document id prefix, a RavenDB session or an embedded server lives on the source side |
| `ServiceControl.Persistence.EFCore` | `EFCoreMigrationTarget`, `EFCoreMigrationTargetReadiness`, one `IMigrationCategoryWriter` per category, `IMigrationSqlDialect`, `MigrationInsert`, `PreparedBatch`, `EFMigrationCheckpointStore`, `MigrationCheckpointExtensions`, the target's three checks, and the two hosted services `CheckpointTableIsReadable` and `RecordHostOpenedOnTarget` | Everything that knows a table, a column width or a provider's parameter ceiling lives on the target side |
| `ServiceControl` (the host) | `RequiredCopyBeforeTheHostOpens`, `MigrationStartup` with its nested `ClosedWindowProgress`, `MigrationStartupCheckRunner`, `MigrationPairIsSupportedCheck`, `EveryRequiredCategoryCanBeCopiedCheck`, `SelectedCategoriesAreCoherentCheck`, `AllowIncompleteCategorySet`, `MigrationSourceReportCommand`, `PersistenceFactory.CreateMigrationSource` | The only place that names both persisters at once, because deciding whether this pair is supported is the one question neither side can answer alone |

Two provider assemblies sit below the EF Core one and hold the only provider-specific migration code: `SqlServerMigrationSqlDialect` and `PostgreSqlMigrationSqlDialect`, each implementing `IMigrationSqlDialect`. They are where a parameter ceiling and a column collation are known.

## The wiring is in place on every SQL instance, migrating or not

Three registrations happen while the host is being built, long before anything decides whether a migration is running, because resolving them is how the copy finds them later:

- **`BasePersistence.RegisterDataStores`**, reached through each provider's `AddPersistence`, registers `IMigrationCheckpointStore` as `EFMigrationCheckpointStore`, `IMigrationTargetReadiness` as `EFCoreMigrationTargetReadiness` and `IMigrationTarget` as `EFCoreMigrationTarget`. All three are singletons. Only the target is genuinely never constructed on an instance that does not migrate: the other two are resolved on every SQL start by the two hosted services registered beside them, `CheckpointTableIsReadable` and `RecordHostOpenedOnTarget`. The RavenDB persister registers none of the three, which is exactly what makes RavenDB a source and never a target.
- **`HostApplicationBuilderExtensions.AddServiceControl`** registers `IMigrationState` as a `CheckpointMigrationState` wrapping whatever `IMigrationCheckpointStore` the persister registered, or none at all on RavenDB. It is a `TryAddSingleton`, so a test can put its own in first.
- **`PersistenceFactory.Create`** copies `Settings.RetryHistoryDepth` onto `PersistenceSettings.RetryHistoryDepth`. Without that one line `RetryHistoryDepthIsSafeCheck` cannot see the value it exists to refuse, because the check runs inside the persister and the setting is read outside it.

**`CheckpointTableIsReadable` is the first migration code to touch the database on any start, migrating or not.** It runs in `StartingAsync`, reads the checkpoint table, and turns any failure into a refusal naming `--setup`. `AddPersistence` is registered before the copy is, and `StartingAsync` runs in registration order, so this refusal comes ahead of every check listed below. It is not one of the target's contributed checks: it is a hosted service on every SQL start, and it exists so that a database whose schema predates this feature stops the host rather than failing later inside a copy.

## Starting the host with the migration on runs the required copy inside host start

*The sequence below is what the code does when the copy can run. On a real build it stops at `EveryRequiredCategoryCanBeCopiedCheck`, because only two of the eighteen categories have both a reader and a writer. Read the whole section before you assume any step after that one has ever executed outside a test.*

```mermaid
sequenceDiagram
    participant Run as RunCommand
    participant Copy as RequiredCopyBeforeTheHostOpens
    participant Start as MigrationStartup
    participant Checks as MigrationStartupCheckRunner
    participant Factory as PersistenceFactory
    participant Source as RavenMigrationSource
    participant Target as EFCoreMigrationTarget
    participant Engine as MigrationEngine

    Run->>Run: registers Copy as a hosted service, then hostBuilder.Build()
    Run->>Run: app.RunAsync
    Run->>Copy: StartingAsync, before any other hosted service starts
    Note over Copy: CheckpointTableIsReadable has already read the<br/>checkpoint table, earlier in the same start
    Copy->>Start: RunRequiredCopy(app.Services, settings)
    Start->>Checks: MigrationPairIsSupportedCheck
    Start->>Factory: CreateMigrationSource(settings)
    Factory-->>Start: a source object, nothing connected yet
    Start->>Start: CopyableCategoryIds(source, target)
    Start->>Checks: EveryRequiredCategoryCanBeCopiedCheck,<br/>then SelectedCategoriesAreCoherentCheck,<br/>then the target's three checks
    Checks->>Target: Open
    Checks->>Source: Open
    Start->>Checks: source.ContributedChecks()
    Checks->>Source: SourceDataVersionIsReadableCheck
    Start->>Engine: RunCategories(the required categories this build can copy)
    Engine-->>Start: one checkpoint per category
    Start->>Start: ReportWhatTheCopyLeftBehind
    Start->>Start: RefuseIfAnyCategoryDidNotComplete
    Start->>Start: CheckpointMigrationState.Seed
    Start-->>Copy: returns, or throws and the host never opens
    Note over Run: RecordHostOpenedOnTarget, an EF Core hosted service,<br/>stamps the target in StartedAsync
```

The order is not incidental, so each position is worth the sentence:

- **`RequiredCopyBeforeTheHostOpens` is why the copy runs inside host start rather than before it.** `RunCommand` registers it only when `Migration/Enabled` is true, before `hostBuilder.Build()`, and `app.RunAsync` then drives its `StartingAsync`. A Windows service that has reported nothing for 30 seconds is killed by the Service Control Manager, and a copy that ran before `RunAsync` would be exactly that silence.
- **`MigrationPairIsSupportedCheck` runs first among the copy's own checks**, because it is the only one answerable from settings without touching a database. It resolves `Migration/SourcePersistenceType` and `PersistenceType` through `PersistenceManifestLibrary`, then compares the source against `MigrationSettings.DefaultSourcePersistenceType` and the target against `PersistenceFactory.SqlPersistenceNames`. An unsupported pair costs a message rather than a connection attempt.
- **`PersistenceFactory.CreateMigrationSource` builds the source without opening it.** It resolves the source persistence by name and casts its configuration to `IMigrationSourceFactory`, which only `RavenPersistenceConfiguration` implements, and refuses by name when the cast fails. What comes back is a `RavenMigrationSource` holding a `RavenReadOnlySourceLifecycle` that has connected to nothing.
- **`CopyableCategoryIds` intersects the two registries.** The source's `SupportedCategoryIds` is the key set of its reader dictionary, and the target's is the key set of its writer dictionary, built inside `EFCoreMigrationTarget`. A category needs both a reader and a writer, so a half-built one is never handed to the engine.
- **`EveryRequiredCategoryCanBeCopiedCheck` then refuses a partial build outright.** Copying part of the required set and opening the host anyway would commit the instance to SQL with required data that nothing goes back for. Today's readers and writers cover `KnownEndpoints` and `EndpointSettings` only, so on a real build this check fails and no migration starts at all. `AllowIncompleteCategorySet` is how the acceptance tests get past it, and nothing in the product registers it. It is a DI marker class, not a setting, and it is not `Migration/AllowIncompleteExit`, which is a setting and does something else entirely.
- **`SelectedCategoriesAreCoherentCheck` reads the options as a check rather than in a constructor.** It calls `MigrationEngineOptions.FromSettings`, which throws when `Migration/OptionalCategories` names a category that does not exist or is not optional. Running it as a step means that typo is reported by name beside the other failures, instead of surfacing as an unrelated startup crash.
- **The target's own checks run before either store is opened.** `EFCoreMigrationTargetReadiness.ContributedChecks` returns `RetryHistoryDepthIsSafeCheck`, `SchemaIsCurrentCheck` and `BodyStorageIsWritableCheck`, in that order. That is the one that needs nothing, then the one that reads the schema, then the one that writes a probe body. The probe is deleted again, so the target's body count does not end up one higher than the source's.
- **The source opens last of all the checks**, because it is the only step that can start a process. `RavenReadOnlySourceLifecycle.Open` does four things. It starts the embedded server or connects to the external one. It hangs `RefuseWrite` on every request, so nothing in the copy can write to the database it is reading. It rejects a client certificate that is expired or not yet valid before the first request rather than after it. It then waits for both databases to load, with a five-minute budget on an embedded one.
- **`SourceDataVersionIsReadableCheck` needs a second round of checks** because it needs a session, and a session needs the source open. It reads the `ServiceControl/DataVersion` document that `DatabaseSetup.StampDataVersion` writes on every RavenDB startup, and refuses four cases by name: no stamp at all, a stamp it cannot parse, a newer major version, and an older major version. Reading those documents into shapes this build understands differently is the loss it is there to prevent.
- **`ClosedWindowProgress` wraps the run, not the engine.** It polls the checkpoint store every 30 seconds, logs each running category's copied, skipped and cursor, and cancels the copy when a category has committed nothing for 30 minutes. It is host-side deliberately: the engine cannot know how long a batch takes, and what follows a stall is a message about an outage rather than about a copy.
- **Two catch blocks around `RunCategories` rewrite the failure before it is reported.** A stall re-reads the checkpoints on the caller's token, so the operator gets the stall message rather than a bare cancellation. A `MigrationCheckpointConflictException` is reported as a second instance pointed at the same database.
- **`RefuseIfAnyCategoryDidNotComplete` is what keeps the host closed.** Any category not `Complete`, `CompleteWithErrors` or `Abandoned`, and any attempted category that reported no checkpoint at all, throws with the counts, the last error and the way back to RavenDB.
- **`CheckpointMigrationState.Seed` runs last, before the host opens.** It reads every checkpoint once and works out whether any selected category is unfinished. A snapshot rather than a live query, because the services meant to consult it sit on the request path and cannot afford a database read each time.
- **`RecordHostOpenedOnTarget` stamps the target, and it is not part of the copy.** It is a hosted service the EF Core persister registers, and it runs in `StartedAsync` on any host start, not only `RunCommand`. It writes the `Migration/HostOpenedOnTarget` setting the first time the host opens on a database that already holds checkpoint rows, and leaves it alone afterwards. That gate on existing checkpoints is the whole reason the marker means "a host has opened since the copy began" rather than "a host ran here once".

## One batch, and every class it passes through

`MigrationEngine.RunCategoryAsync` is the whole copy for one category. Each step below is delegated, and the reason is always the same shape: the engine must not learn anything about either store.

| Step | Where the work happens | Why not in the engine |
| --- | --- | --- |
| Read the category's saved row | `EFMigrationCheckpointStore.Read` | The checkpoint table lives in the target's database, so only the target's assembly can query it |
| Pass over a category that is done | `MigrationCategoryStateExtensions.IsFinished` | One definition of "finished" shared by the engine, the host's refusal and the state seed, so the three cannot drift |
| Hold a category behind another | `MigrationCategory.MustFollow`, from `MigrationCategoryRegistry.All` | Ordering is data, not code: group comments follow archived messages because the registry says so |
| Choose the batch size | `EFCoreMigrationTarget.BatchSizeFor`, which asks the category's `IMigrationCategoryWriter` | Only the target knows its provider's parameter ceiling and its own column count |
| Count the source once, on the first run | `RavenMigrationSource.Count` | It streams the category rather than reading collection statistics, because the count has to be over exactly the rows `Read` will hand back. A total that included rows `Read` leaves out would halt the category for a shortfall that never happened |
| Read a batch after the cursor | the category's `IMigrationCategoryReader`, through `RavenDocumentStream.ByPrefix` | The stream opens a no-tracking session, refuses a cursor whose document no longer exists (RavenDB would otherwise silently start after a non-existent id and skip rows), and advances the cursor on every document it sees, projected or not |
| Fetch message bodies | `MigrationEngine.FetchBodiesWithRetry`, calling `IMigrationSource.ReadBody` | The retry policy is store-neutral: three attempts, a backoff between them, and `IsDefect` exceptions such as `NotSupportedException` not retried at all |
| Turn documents into rows | the category's `IMigrationCategoryWriter.Prepare`, returning a `PreparedBatch` | Only the writer knows which columns are `NOT NULL`, which keys are capped, and what the running product would delete anyway. It writes nothing: the insert it returns runs later, inside the target's transaction |
| Insert the rows and save the checkpoint | `EFCoreMigrationTarget.Write`: `PreparedBatch.Insert` calls the provider's `IMigrationSqlDialect.InsertMissing`, built on `MigrationInsert`, then `AlreadyPresentIn`, `MigrationCheckpoint.Extend` and `MigrationCheckpointExtensions.UpsertCheckpoint`, all inside one transaction under the provider's execution strategy | This is the guarantee in [one batch, and why nothing provisional is ever saved](ravendb-to-sql-migration-overview.md#one-batch-and-why-nothing-provisional-is-ever-saved). The counts are added to the checkpoint inside the same transaction as the rows they describe, so nothing travels back through the engine to be saved later |
| Decide whether to halt | `HaltThreshold.Exceeded` and `HaltThreshold.MostOfItWasLost`, on counters the engine keeps per run | Both the percentage and the floor must be exceeded, and `MostOfItWasLost` catches a category too small to reach the floor. The counters are deliberately not stored, so a restart with the cause fixed does not re-trip on its first batch |
| Settle the category | `MigrationEngine.Settle` | The engine logs before it settles, because the checkpoint store shares the target's database and a failed save would otherwise hide the cause |

Two details in that chain are worth following because they show how a rule travels between classes:

- **`MigrationCheckpoint.Extend` refuses a batch whose skips are not all explained.** The target passes a count per `MigrationSkipReason`, and the save throws unless those sum exactly to the skipped count, in either direction. `EFCoreMigrationTarget.Write` makes the same bargain in the other direction with `AlreadyPresentIn`: every row in the batch is copied, skipped or already present, and a miscount throws instead of quietly shrinking the halt threshold's denominator.
- **`EndpointSettingsWriter` reads the `KnownEndpoints` checkpoint row before it decides anything.** It skips settings for an endpoint the target does not know, because the heartbeat sync would delete them shortly after startup, and those skips are benign. But if the `KnownEndpoints` copy itself dropped rows, an unknown endpoint can be this migration's own doing rather than the source's, so `PreparedBatch.SkipsReflectTheSource` goes false, `BenignSkipCount` returns zero, and those skips start counting toward a halt again. `EndpointNotKnown` is the only reason `MigrationSkipReasonExtensions.IsBenign` accepts, so that one writer is where the whole benign-skip exemption lives.

## The read-only commands use the source and nothing else

`--migration-source-report` runs `MigrationSourceReportCommand`, which calls `PersistenceFactory.OpenMigrationSource` and then `IMigrationSource.Describe` and `Inventory`. Those two members exist for that command alone: the copy never calls either. `Describe` returns `MigrationSourceFact` values, and each carries the setting key its value came from wherever there is one, which is what lets the report tell an operator not just that the database name is wrong but which setting to change. The `Mode` fact has no setting key and prints without one. `Inventory` reports every collection in both source databases, including data no category copies, so the operator sees the whole source rather than the part we intend to move.

## What is built but not yet on a call path

*Listed because a reader tracing these classes will otherwise assume the behaviour above them is live.*

- **Sixteen of the eighteen categories have no reader and no writer.** `RavenMigrationSource` holds `KnownEndpointsReader` and `EndpointSettingsReader`, and `EFCoreMigrationTarget` holds the matching two writers. Every other category throws `NotSupportedException` on both sides, and `EveryRequiredCategoryCanBeCopiedCheck` therefore refuses every real migration.
- **The `Migration/Enabled = false` exit gate is not wired.** `IMigrationTargetReadiness.HasHostOpened` and `Settings.MigrationAllowIncompleteExit` both exist and nothing reads either, so the branch in [turning migration mode off is a gated startup too](ravendb-to-sql-migration-overview.md#turning-migration-mode-off-is-a-gated-startup-too) does not run yet and an instance with an outstanding category simply starts. `RecordHostOpenedOnTarget` does run, so the marker that gate will need is already being written. Nothing anywhere sets `MigrationCategoryState.Abandoned`.
- **`IMigrationState.AnyCategoryIncomplete` is seeded and has no production reader.** The services that are meant to stand down while a copy is outstanding still run as normal.
- **`RavenMigrationSource.ReadBody` throws for every category**, so no `CarriesBodies` category can be copied yet, which includes the required failed messages. `NotSupportedException` is on the engine's non-retryable list, so a body-carrying category would not make three attempts and would not skip the message: it would halt the category.
- **The background copy of optional categories has no caller.** `MigrationStartup.RunRequiredCopy` selects the optional ids only so `CheckpointMigrationState.Seed` knows which categories count as outstanding, and nothing runs them. The engine's throttle pause applies only to optional categories, so nothing in the product ever waits between batches today.
- **`CheckpointMigrationState.Recompute` is public for that background copy to call as each category settles.** Today only `Seed` calls it.
- **`MigrationSkipReason.PastRetention` has no writer.** The enum value exists and `IsBenign` does not accept it, so the first writer to use it will count past-retention skips toward a halt unless that is changed too.
- **There is no migration custom check and no migration domain event.** Progress reaches an operator only through `ILogger` output.
