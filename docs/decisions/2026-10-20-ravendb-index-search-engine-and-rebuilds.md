# ServiceControl owns the search engine of its indexes, not of its databases

- Date: 2026-10-20
- Status: Proposed
- Implementation: [#6034](https://github.com/Particular/ServiceControl/pull/6034), [#6035](https://github.com/Particular/ServiceControl/pull/6035), [#6036](https://github.com/Particular/ServiceControl/pull/6036), [#6013](https://github.com/Particular/ServiceControl/pull/6013), [#6098](https://github.com/Particular/ServiceControl/pull/6098). Operator guidance: [Particular/docs.particular.net#8577](https://github.com/Particular/docs.particular.net/pull/8577). Follow-up: [#6015](https://github.com/Particular/ServiceControl/issues/6015).
- Builds on: [#4484](https://github.com/Particular/ServiceControl/pull/4484) (6.0.0), [#5833](https://github.com/Particular/ServiceControl/pull/5833) (6.20.0), [#5946](https://github.com/Particular/ServiceControl/pull/5946) (6.21.1).

## Context

ServiceControl stores its data in RavenDB. Each static index uses a search engine, Corax or Lucene. RavenDB resolves the engine of an index from the index configuration, the database settings, and the server default, in that order, and stores the result with the index. A database default therefore does not change an existing index. It applies only when an index is created, reset, or replaced.

At every start-up, ServiceControl deploys its index definitions. When a definition differs from the one on the server, RavenDB builds a side-by-side replacement and swaps it in once it is no longer stale. On a large audit database this takes days, competes with ingestion, and needs storage for both indexes. A locked index does not receive the new definition.

The engine history is:

- 6.0.0 ([#4484](https://github.com/Particular/ServiceControl/pull/4484)) wrote Corax into the database settings of every database that had none, at every start-up.
- 6.20.0 ([#5833](https://github.com/Particular/ServiceControl/pull/5833)) creates new databases with Lucene, because load tests showed smaller indexes, less RAM, and better speed for ServiceControl's definitions. Existing databases kept the Corax pin, and migrating an index became an operator decision because it triggers a rebuild. The release also changed the audit `MessagesViewIndex` and `MessagesViewIndexWithFullTextSearch` definitions without an upgrade-guide entry.
- 6.21.1 ([#5946](https://github.com/Particular/ServiceControl/pull/5946)) keeps an engine that is set on an index on the server.

Three problems remained in 6.21.2:

1. The Corax pin cannot tell "never pinned" from "the operator removed the setting". RavenDB Studio removes a database setting whose value equals the server default, so the documented "Migrating the whole database" procedure on a Lucene server deleted the keys and ServiceControl restored Corax at the next start.
2. An index without an engine of its own follows the database default when RavenDB rebuilds it. In a pinned database, a Lucene index without its own setting moved back to Corax.
3. Neither developers nor operators get a signal before a definition change starts a rebuild. 6.20.0 showed that an upgrade-guide entry depends on someone noticing the change.

Adding an engine to the configuration of an unchanged index is itself a definition change, and RavenDB rebuilds the index even when the engine is the same. Any rule that sets an engine must do so only where a rebuild happens anyway. An operator's engine choice, on an index or on the database, must survive start-up. A rebuild cannot be made cheap, only predictable.

## Decision

ServiceControl no longer writes search engine settings to an existing database ([#6034](https://github.com/Particular/ServiceControl/pull/6034)). New databases are still created with Lucene. Settings written by 6.0.0 to 6.21.2 are not removed, because removing them is also an operator decision.

`IndexDeployment` resolves the engine of each index before deployment with these rules ([#6035](https://github.com/Particular/ServiceControl/pull/6035)), applied in order:

1. An engine set in the definition in code wins.
2. An engine set on the index on the server, or on its pending replacement, is kept. The pending replacement is checked first, because it holds the operator's latest choice.
3. A new index, or an index that RavenDB rebuilds because its definition changed, uses Lucene. When the database setting is Lucene, the index inherits it and gets no setting of its own. Otherwise Lucene is set on the index.
4. An unchanged index without an engine of its own is left alone.

Rule 3 is the point of leverage. A Corax index without its own setting moves to Lucene at its next rebuild, which the definition change triggers anyway, so the migration costs no extra rebuild. A Lucene index never falls back to Corax.

A rebuild is made visible instead of avoided. An approval test renders every index definition the persisters deploy, so a definition change fails review until the upgrade guide records it ([#6013](https://github.com/Particular/ServiceControl/pull/6013)). At start-up, `IndexDeployment` reports each index's engine, each rebuild, and each locked index ([#6036](https://github.com/Particular/ServiceControl/pull/6036)). Operators get general guidance for upgrades that rebuild indexes ([docs#8577](https://github.com/Particular/docs.particular.net/pull/8577)).

## Consequences

- Existing databases keep whatever engine settings they have, including a Corax pin from 6.0.0 to 6.21.2. Accepted, because the database default no longer decides the engine of a rebuilt index. The setting still applies to auto indexes and to a reset made in Studio.
- An index migrates from Corax to Lucene without operator action at its next definition change. The upgrade guide entry for the first release that changes a definition on a pinned database must say so, and the documented "Migrating the whole database" procedure needs to be checked against the new behavior.
- An index with Corax set on the index itself stays on Corax across definition changes (rule 2). Accepted: it is an explicit operator choice.
- A pending replacement started by 6.20.0 to 6.21.2 for an index without an engine of its own, in a database whose setting is not Lucene, has no engine of its own either. After the upgrade, rule 3 sets Lucene on the index, so the deployed definition no longer matches that replacement, and RavenDB is expected to build a new one, losing the progress made so far. #6035 does not discuss this case, and no test covers it. Mitigation: the upgrade guide tells operators to let a running rebuild finish before upgrading. Keeping the replacement on Corax would leave that index on Corax until its next definition change.
- A locked index whose definition changed, `LockedIgnore` or `LockedError`, fails start-up before any index is deployed ([#6036](https://github.com/Particular/ServiceControl/pull/6036)), so a rebuild never starts on an index the operator locked. In maintenance mode ([#6098](https://github.com/Particular/ServiceControl/pull/6098)) the error is logged, the locked index is not deployed, and start-up continues, so the operator can unlock the index in RavenDB Studio after an upgrade and start ServiceControl again. Without this, an embedded database stops with the failed start-up and Studio cannot be reached. The search engine guidance tells operators to check the upgrade guide for changes to locked indexes and to unlock an index whose definition changed. It does not yet say to unlock before upgrading, or that maintenance mode is how to reach RavenDB Studio when start-up already failed. That needs a docs.particular.net update.
- A false positive in change detection on a database whose setting is not Lucene adds Lucene to an unchanged index and starts the rebuild that rule 4 exists to prevent. Mitigated by comparing with the deployed conventions and by tests that assert an unchanged index is not rebuilt.
- The approval test adds a re-approval and an upgrade-guide entry to every index change. Accepted. Re-approving is a file copy, so the test is a reminder, not a gate.
- An operator still learns about a rebuild only from the logs once it has started. The document count, an opt-in setting that refuses an unplanned rebuild, and a pre-upgrade check in ServiceControl Management remain open in [#6015](https://github.com/Particular/ServiceControl/issues/6015).

## Alternative approaches

- Keep pinning existing databases, but only once, with a marker that records that the pin was applied. This would preserve the intent of 6.0.0 without overriding a later removal. It is rejected because the pin has no benefit: RavenDB stores the engine with each index, so the database default does not protect existing indexes from anything.
- Set Lucene on every index at start-up. This migrates every installation in one step. It is rejected because adding the setting changes every definition and rebuilds every index on every existing database at once, which is exactly the multi-day rebuild 6.20.0 left to the operator.
- Set the database default to Lucene during the upgrade. This makes rebuilt and new indexes inherit Lucene without a per-index setting. It is rejected because it overrides an operator's database setting, the same flaw as the Corax pin in the other direction.
- Keep following the database default for rebuilt indexes, as 6.21.1 did. This changes nothing that an operator configured. It is rejected because a definition change in a pinned database silently moves a Lucene index back to Corax, and because it wastes the one rebuild in which switching engine is free.
- Ask RavenDB whether an index will change with `IndexHasChangedOperation`, as [#6015](https://github.com/Particular/ServiceControl/issues/6015) proposes, instead of a client-side `Compare`. It uses the server's own rule. It is deferred: it needs one round trip per index, and it compares the definition after the engine is resolved, so it cannot inform the rule that resolves it.
- Rely on upgrade-guide entries alone to warn about rebuilds. It costs nothing in code. It is rejected because 6.20.0 shows an entry depends on someone noticing the change. The approval test makes the change visible in review.
- Gate index changes in CI or at release time: a pull request label with a required acknowledgement, or a release check that compares approved files with the previous tag. A release check would have caught 6.20.0, because it sees changes spread across several pull requests. These remain candidates for a follow-up; they were proposed in the [#6013](https://github.com/Particular/ServiceControl/pull/6013) review and not implemented.
