# Database schema and index changes

Applies to any change that alters a RavenDB index definition or a relational table schema:

- RavenDB indexes: `src/ServiceControl.Persistence.RavenDB/**/Indexes/**`, `src/ServiceControl.Audit.Persistence.RavenDB/Indexes/**`, and the document conventions in `src/ServiceControl.RavenDB/**`
- EF Core migrations, entities, and their configuration: `src/ServiceControl.Persistence.EFCore*/**` and `src/ServiceControl.Audit.Persistence.EFCore*/**`

## Purpose

ServiceControl error and audit instances keep their data in a database. The persisters use RavenDB, or SQL Server or PostgreSQL through Entity Framework Core. A change to an index definition or a table schema may look small in a pull request, but it changes the database of every installation at the first upgrade that ships it. The operator doesn't see the change before then and cannot plan for it. This guideline sets the rules for such a change.

## Why a change is expensive

- RavenDB compares the deployed index definition with the definition on the server at every start-up. A difference in the map, reduce, fields, analyzers, or index configuration makes RavenDB build a replacement index side by side. On a large audit database the rebuild takes days, ingestion and indexing are degraded while it runs, and the database needs storage for both indexes. Under constant ingestion the replacement can stay stale, and RavenDB does not swap it in.
- An operator can lock a RavenDB index, for example after migrating it from Corax to Lucene. A locked index does not receive a changed definition. From 6.22.2, a locked index whose definition changed stops the start-up with an error that names the index, so the upgrade fails until the operator unlocks it. See the [RavenDB index search engine and rebuilds ADR](../decisions/2026-10-20-ravendb-index-search-engine-and-rebuilds.md).
- A relational migration runs during setup (`--setup`), which installing and upgrading an instance run, with the privileges of the account that runs it. A new index or a changed column scans or rewrites the whole table, holds locks, and needs transaction log and disk space. A normal start fails when migrations are pending.

Storage migrates forward only: a newer version upgrades the database, and an older version is not expected to read it again. This guideline adds the rules for a migration inside a supported major version.

## Rules

1. Treat a change to an index definition or a table schema as a breaking change, also when no API changes. It requires the same planning: a major release, or a staged introduction.
2. When the change must ship in a minor release, make it opt-in. The instance keeps the current index or schema until the operator enables the change, for example through a setting. The next major release makes the change the default.
3. Record the change in the [upgrade guide](https://docs.particular.net/servicecontrol/upgrades/) before the release. Name the changed indexes or tables, the expected effect (rebuild, migration, degraded ingestion, storage, required privileges), how to opt in, and the action for an operator who locked a RavenDB index. Mention the change in the release notes.
4. Do not change an index or a schema in a patch release unless the patch fixes a bug in that index or table.
5. Keep the approval tests current, so a change is a visible decision in the pull request. The RavenDB persistence test projects approve every deployed index definition (`IndexDefinitionApprovals`), and the SQL Server and PostgreSQL persistence test projects approve the migration steps that can block writes on a large table (`SlowMigrationOperationApprovals`). See [Persistence tests](../testing.md#persistence-tests). When an approval changes or a migration is added, write the upgrade guide entry in the same pull request.
6. Check for implicit changes. A database client or provider upgrade, a new default search engine, or a new document convention can change the deployed definition without a change to an index class or an entity.
7. A data migration must never lose data, and must be able to continue after it is stopped.

## Pull request checklist

Add this list to a pull request that changes an index definition or a table schema:

- [ ] The change ships in a major release, or it is opt-in in a minor release
- [ ] The upgrade guide names the changed indexes or tables, the expected effect, and how to opt in
- [ ] The upgrade guide tells an operator with a locked RavenDB index what to do
- [ ] The release notes mention the change
- [ ] The approval files, or the EF Core migration and model snapshot, are updated in the same pull request

## Background

In ServiceControl 6.20.0, the audit `MessagesViewIndex` and `MessagesViewIndexWithFullTextSearch` definitions received a new field. The 6.19 to 6.20 upgrade guide did not mention it and told operators with locked indexes that no action was required. Operators who unlocked the index on 6.21.2 got a rebuild that took days, without a visible cause.
