# Prefer explicit persistence operations

Applies to: every change under `src/*Persistence*/**`, and any change that adds or changes a persistence operation or unit of work.

## Rules

- A persistence operation whose inputs fit in one method call must be a direct data-store method.
- A data-store method must own and dispose its EF Core scope or context or its RavenDB session, accept a `CancellationToken`, and commit before it returns.
- A data-store method must return detached entity snapshots, so callers never mutate tracked entities as an implicit persistence command.
- An atomic operation that can hit a concurrency conflict must document its provider guarantees and translate expected provider exceptions into explicit domain outcomes, such as a unique-key or optimistic-concurrency conflict that is part of its normal contract.
- A unit of work must be used only when a caller composes several writes into one atomic batch.
- A new unit-of-work API must provide an `I...UnitOfWorkFactory` with a `StartNew` method, a `Complete(CancellationToken)` commit method, `IAsyncDisposable` lifetime ownership, and explicit operation-recording methods instead of mutation of tracked return values.
- A new unit-of-work API must document its commit, abandon, repeated-completion, and concurrency behavior.
- Code must not introduce generic `IDataSessionManager`-style abstractions or persistence managers with hidden call-order protocols.

## Rationale

[#5763](https://github.com/Particular/ServiceControl/pull/5763) introduced these rules. It removes manager objects and implicit state in favor of atomic data-store operations that handle concurrency conflicts explicitly.
