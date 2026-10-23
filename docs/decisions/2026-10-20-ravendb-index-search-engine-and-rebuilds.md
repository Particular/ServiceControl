# ServiceControl owns the search engine of its indexes, not of its databases

- Date: 2026-10-20
- Status: Proposed
- Implementation: [#6034](https://github.com/Particular/ServiceControl/pull/6034), [#6035](https://github.com/Particular/ServiceControl/pull/6035), [#6036](https://github.com/Particular/ServiceControl/pull/6036), [#6013](https://github.com/Particular/ServiceControl/pull/6013). Operator guidance: [Particular/docs.particular.net#8577](https://github.com/Particular/docs.particular.net/pull/8577). Follow-up: [#6015](https://github.com/Particular/ServiceControl/issues/6015).
- Builds on: [#4484](https://github.com/Particular/ServiceControl/pull/4484) (6.0.0), [#5833](https://github.com/Particular/ServiceControl/pull/5833) (6.20.0), [#5946](https://github.com/Particular/ServiceControl/pull/5946) (6.21.1).

## Context

ServiceControl stores its data in RavenDB. Each static index uses a search engine, Corax or Lucene. RavenDB resolves the engine of an index from, in order, the index configuration (`Indexing.Static.SearchEngineType`), the database settings, and the server default. RavenDB stores the resolved engine with the index. A database default therefore does not change an existing index. It applies only when an index is created, reset, or replaced.

At every start-up, ServiceControl deploys its index definitions. RavenDB compares each deployed definition with the one on the server, including the index configuration. When they differ, RavenDB builds a side-by-side replacement, `ReplacementOf/<index>`, and swaps it in once it is no longer stale. The replacement processes every document in the database. On a large audit database this takes days, competes with ingestion, and needs storage for both indexes until the swap. A locked index (`LockedIgnore`) does not receive the new definition. A `LockedError` index makes start-up fail with `IndexCreationException`.

The engine history is:

- 6.0.0 ([#4484](https://github.com/Particular/ServiceControl/pull/4484)) wrote `Indexing.Auto.SearchEngineType` and `Indexing.Static.SearchEngineType` with value Corax into every database that had no such settings, at every start-up.
- 6.20.0 ([#5833](https://github.com/Particular/ServiceControl/pull/5833)) creates new databases with Lucene, because load tests showed Lucene indexes are smaller, use less RAM, and are faster for ServiceControl's definitions. Existing databases kept the Corax pin. Migrating an existing index is an operator decision, made in RavenDB Studio, because it triggers a rebuild. 6.20.0 also changed the audit `MessagesViewIndex` and `MessagesViewIndexWithFullTextSearch` definitions without an upgrade-guide entry.
- 6.21.1 ([#5946](https://github.com/Particular/ServiceControl/pull/5946)) added `IndexDeployment`, which keeps an engine set on an index on the server. Before it, the next start-up reset every index migrated in Studio back to the Corax database default.

Three problems remained in 6.21.2:

1. The Corax pin cannot tell "never pinned" from "the operator removed the setting". RavenDB Studio removes a database setting whose value equals the server default. An operator who followed the documented "Migrating the whole database" procedure on a server whose default is Lucene therefore deleted the keys, and ServiceControl restored Corax at the next start, also in maintenance mode.
2. An index without an engine of its own follows the database default when RavenDB rebuilds it after a definition change. In a pinned database, a Lucene index without its own setting, for example one reset under a Lucene default, moved back to Corax.
3. Neither developers nor operators get a signal before a definition change starts a rebuild. 6.20.0 showed that an upgrade-guide entry depends on someone noticing the change.

The decision has to respect these constraints:

- Adding an engine to the configuration of an unchanged index is itself a definition change, and RavenDB rebuilds the index even when the engine is the same. Any rule that sets an engine must do so only where a rebuild happens anyway.
- An operator's engine choice, on an index or on the database, must survive start-up.
- A rebuild cannot be made cheap. It can only be made predictable: known to developers before release, and to operators before and during the upgrade.
- Index deployment runs at every start-up, independent of `IngestAuditMessages` and `IngestErrorMessages`.

## Decision

ServiceControl no longer writes search engine settings to an existing database ([#6034](https://github.com/Particular/ServiceControl/pull/6034)). Both `DatabaseSetup` classes drop `UpdateDatabaseSettings`. New databases are still created with Lucene. Settings written by 6.0.0 to 6.21.2 are not removed, because removing them is also an operator decision.

`IndexDeployment` resolves the engine of each index before deployment with these rules ([#6035](https://github.com/Particular/ServiceControl/pull/6035)), applied in order:

1. An engine set in the definition in code, through `SearchEngineType` or `Configuration`, wins.
2. An engine set on the index on the server, or on its pending replacement, is kept. The pending replacement is checked first, because it holds the operator's latest choice.
3. A new index, or an index that RavenDB rebuilds because its definition changed, uses Lucene. When the database setting is Lucene, the index inherits it and gets no setting of its own. Otherwise Lucene is set on the index.
4. An unchanged index without an engine of its own is left alone.

A definition change is detected with `IndexDefinition.Compare` against the deployed definition, masked to maps, reduce, fields, additional sources, and additional assemblies. The comparison uses the store conventions, because `SaveEnumsAsIntegers` changes how enum literals render in a map.

Rule 3 is the point of leverage. A Corax index without its own setting moves to Lucene at its next rebuild, which the definition change triggers anyway, so the migration costs no extra rebuild. A Lucene index never falls back to Corax.

At start-up `IndexDeployment` reports what it decides ([#6036](https://github.com/Particular/ServiceControl/pull/6036)): the database engine setting, the engine of each index and where it comes from, a warning for each rebuild with the differences that cause it, a warning for each locked index, and an error for a locked index whose definition changed.

Developers see a definition change before release ([#6013](https://github.com/Particular/ServiceControl/pull/6013)). An `IndexDefinitionApprovals` test in each RavenDB persistence test project renders every index the persister deploys, without a RavenDB server. The rendering uses `DocumentStoreConventions.Create()`, the same factory the embedded and external document stores use. [`docs/testing.md`](../testing.md) states that a failing test means recording the changed indexes in the upgrade guide of the next release before approving.

Operators get general guidance for upgrades that rebuild indexes ([docs#8577](https://github.com/Particular/docs.particular.net/pull/8577)): how side-by-side replacement works, the storage it needs, upgrading audit instances one at a time with ingestion disabled, and postponing the rebuild of a locked index until retention has purged part of the database.

## Consequences

- Existing databases keep whatever engine settings they have, including a Corax pin from 6.0.0 to 6.21.2. Accepted, because the database default no longer decides the engine of a rebuilt index. The setting still applies to auto indexes and to a reset made in Studio.
- An index migrates from Corax to Lucene without operator action at its next definition change. The first release that changes a definition on a pinned database therefore also changes the engine of that index. The upgrade guide entry for that release must say so. Lucene indexes are smaller than Corax indexes, so the storage estimate in the operator guidance stays conservative.
- An index with Corax set on the index itself stays on Corax across definition changes (rule 2). Accepted: it is an explicit operator choice.
- A pending replacement started by a version from 6.20.0 to 6.21.2, for an index without an engine of its own in a database whose setting is not Lucene, has no engine of its own either. After the upgrade, rule 3 sets Lucene on the index, so the deployed definition no longer matches that replacement, and RavenDB is expected to build a new one, losing the progress made so far. #6035 does not discuss this case, and no test covers it. Mitigation: the upgrade guide of the release tells operators to let a running rebuild finish before upgrading. The alternative, keeping the replacement on Corax, would leave that index on Corax until its next definition change.
- An index locked with `LockedError` whose definition changed makes start-up fail with `IndexCreationException`, after the error that #6036 logs. This is existing behavior, covered by `IndexSetupTests`. Mitigation: the upgrade guide covers `LockedError` next to `LockedIgnore`, which only postpones the change.
- Existing databases are no longer pinned to Corax at start-up, and a rebuilt index changes engine without operator action. Both are visible to operators, so the release that ships them needs an upgrade-guide entry, and the documented "Migrating the whole database" procedure needs to be checked against the new behavior.
- A false positive in change detection on a database whose setting is not Lucene adds Lucene to the index configuration and starts the rebuild that rule 4 exists to prevent. Mitigated by using the deployed conventions and by `IndexSetupTests`, which assert that an unchanged index without a setting is not rebuilt.
- Every start-up costs one `GetIndexesOperation`, one `GetDatabaseRecordOperation`, and one comparison per index. Negligible next to the deployment itself.
- The approval test raises the cost of an index change: a re-approval and an upgrade-guide entry. Accepted. Re-approving is still only a file copy, so the test is a reminder, not a gate.
- An operator still learns about a rebuild only from the logs once it has started. The document count, an opt-in setting that refuses an unplanned rebuild, and a pre-upgrade check in ServiceControl Management remain open in [#6015](https://github.com/Particular/ServiceControl/issues/6015).

## Alternative approaches

- Keep pinning existing databases, but only once, with a marker that records that the pin was applied. This would preserve the intent of 6.0.0 without overriding a later removal. It is rejected because the pin has no benefit: RavenDB stores the engine with each index, so the database default does not protect existing indexes from anything.
- Set Lucene on every index at start-up. This migrates every installation in one step. It is rejected because adding the setting changes every definition and rebuilds every index on every existing database at once, which is exactly the multi-day rebuild 6.20.0 left to the operator.
- Set the database default to Lucene during the upgrade. This makes rebuilt and new indexes inherit Lucene without a per-index setting. It is rejected because it overrides an operator's database setting, the same flaw as the Corax pin in the other direction.
- Keep following the database default for rebuilt indexes, as 6.21.1 did. This changes nothing that an operator configured. It is rejected because a definition change in a pinned database silently moves a Lucene index back to Corax, and because it wastes the one rebuild in which switching engine is free.
- Ask RavenDB whether an index will change with `IndexHasChangedOperation`, as [#6015](https://github.com/Particular/ServiceControl/issues/6015) proposes, instead of a client-side `Compare`. It uses the server's own rule. It is deferred: it needs one round trip per index, and it compares the definition after the engine is resolved, so it cannot inform the rule that resolves it.
- Rely on upgrade-guide entries alone to warn about rebuilds. It costs nothing in code. It is rejected because 6.20.0 shows an entry depends on someone noticing the change. The approval test makes the change visible in review.
- Gate index changes in CI or at release time: a pull request label with a required acknowledgement, or a release check that compares approved files with the previous tag. A release check would have caught 6.20.0, because it sees changes spread across several pull requests. These remain candidates for a follow-up; they were proposed in the [#6013](https://github.com/Particular/ServiceControl/pull/6013) review and not implemented.
