# The namespace of a RavenDB index class decides the database it is deployed to

- Date: 2026-11-12
- Status: Accepted
- Implementation: [#6099](https://github.com/Particular/ServiceControl/pull/6099), backported to 6.22.3 in [#6118](https://github.com/Particular/ServiceControl/pull/6118).
- Fixes: [#6123](https://github.com/Particular/ServiceControl/issues/6123).
- Related: [RavenDB index search engine and rebuilds](2026-10-20-ravendb-index-search-engine-and-rebuilds.md), which explains why a query must not create an auto index.

## Context

The error instance with RavenDB persistence uses two databases: the error database, and the throughput database that holds the licensing data (`EndpointDocument`). Both are set up by `DatabaseSetup` in `ServiceControl.Persistence.RavenDB`. The audit instance uses one database.

`IndexDeployment.FindIndexes` discovers every `AbstractIndexCreationTask` in an assembly. Until #6099, `DatabaseSetup` deployed all of them to the error database. The throughput database had no static index, even though `EndpointIndex` was written for it. The licensing queries in `LicensingDataStore` were dynamic, and RavenDB served them with auto indexes in the throughput database, which the Lucene migration cannot reach (#6123).

The constraints:

- An index class carries no information about the database it belongs to. Nothing in the index, the query, or the approval test tied `EndpointIndex` to the throughput database, so the wrong placement went unnoticed since 5.4.0.
- A query that names a static index (`Query<T, TIndex>`) on a session opened for a database where that index does not exist fails. A dynamic query instead creates an auto index silently in production.
- Moving an index from one database to another leaves the old copy behind, and an operator might have locked it after migrating it to Lucene.
- The fix was backported to a patch release, so it had to stay small.

## Decision

`DatabaseSetup` splits the discovered indexes by the namespace of the index class ([#6099](https://github.com/Particular/ServiceControl/pull/6099)):

- An index class whose namespace is exactly `ServiceControl.Persistence.RavenDB.Throughput.Indexes`, the namespace of `EndpointIndex`, is deployed to the throughput database.
- Every other index class in the assembly is deployed to the error database.

`IndexDeployment.CreateIndexesAsync` takes the target database as a parameter. Before deploying, `DatabaseSetup` deletes each throughput index from the error database, if it is there, and unlocks it first, because RavenDB ignores the deletion of a `LockedIgnore` index and rejects that of a `LockedError` index.

The licensing queries go through `EndpointIndex` with `Query<EndpointDocument, EndpointIndex>` on a session opened for the throughput database.

## Consequences

- The namespace check is weak: it is a naming convention that the compiler does not enforce, and it fails silently. Accepted for a patch, with the risks and mitigations below.
- An index class for the throughput database placed in another namespace, including a nested one such as `...Throughput.Indexes.Reports`, is deployed to the error database. Mitigation: a query that names the index on the throughput database fails, and the RavenDB test servers make a dynamic query throw ([#6120](https://github.com/Particular/ServiceControl/pull/6120)), so a test that exercises the query fails. A query that no test exercises is not covered.
- An existing error index moved into `ServiceControl.Persistence.RavenDB.Throughput.Indexes` is deleted from the error database at the next start-up and built from scratch in the throughput database. No test catches this. Mitigation: the rules for adding an index below forbid moving an index class between namespaces, and a namespace change shows in review as a changed file.
- Renaming or moving `EndpointIndex` moves the namespace the check compares with, and every other throughput index then goes to the error database. Mitigation: the same as for a misplaced index.
- The index definition approval test ([#6013](https://github.com/Particular/ServiceControl/pull/6013)) renders the definitions without the target database, so a change of placement does not change the approved file. Follow-up: render the target database in the approved file, which turns a placement change into a visible approval change.
- Existing throughput databases get `EndpointIndex` as a new index over one document per endpoint and source, which builds quickly. The auto indexes they contain are deleted by RavenDB once idle, or by an operator in RavenDB Studio.

## Adding a RavenDB index

1. Decide which database holds the documents the index maps. In the audit persister, there is only one database. In the error persister, licensing documents (`EndpointDocument`) are in the throughput database, and everything else is in the error database.
2. For the throughput database, put the class in `src/ServiceControl.Persistence.RavenDB/Throughput/Indexes/` with the namespace `ServiceControl.Persistence.RavenDB.Throughput.Indexes`, not a nested one. For the error database, use any other namespace in the persister.
3. Query through the index by type, `Query<TDocument, TIndex>`, on a session opened for the database that holds it. Do not rely on a dynamic query: it creates an auto index in production.
4. Add or extend a test that runs the query. It fails if the index is in the wrong database, or if the query is dynamic.
5. Approve the new definition in `IndexDefinitionApprovals`, and check [Persistence tests](../testing.md#persistence-tests) for when the change needs an upgrade guide entry.
6. Never move an existing index class to another namespace. To move an index between databases, add the deletion of the old copy to `DatabaseSetup`, as `DeleteThroughputIndexesFromErrorDatabase` does, and treat it as an index change in the upgrade guide.

## Alternative approaches

- Mark each index with the database it belongs to, through an attribute or a marker interface. This makes the placement explicit at the class and lets a test check that every index has exactly one marker. It is the point of leverage for a future change. It is not done here because it adds a new type for every database and touches every index class, which is more than a patch needs.
- Move the throughput indexes to their own assembly, or derive them from a base class per database. The compiler then enforces the split. It is rejected for the patch because it moves code between projects, or changes the base type of every index class.
- List the throughput indexes explicitly in `DatabaseSetup`. A new throughput index then has to be added to the list, which is as easy to forget as the namespace, but a forgotten entry deploys the index to the error database in the same silent way. It does not improve on the namespace check, and adds a second place to edit.
- Keep deploying every index to every database. It needs no rule at all. It is rejected because each database would build indexes over collections it does not hold, and the error database would keep an `EndpointIndex` that no query uses.
