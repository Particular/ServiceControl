# Coding and design guidelines

This document lists the coding and design guidelines for ServiceControl. When it conflicts with other coding and design guidelines, this document takes precedence for ServiceControl.

## Prefer Microsoft abstractions

Microsoft maintains a number of abstractions for common cross-cutting concerns in the [framework libraries](https://docs.microsoft.com/en-us/dotnet/standard/framework-libraries) and in the `Microsoft.Extensions.*` packages. Where such an abstraction exists, use it instead of any other abstraction, including one of our own.

Relying on relatively stable abstractions makes it easier to isolate the application from third-party dependencies. It also keeps parts of the application isolated from each other, for example running the embedded database in maintenance mode without starting the NServiceBus endpoint.

For example:

- Prefer `IHostBuilder` extensions over NServiceBus features: Add a new feature as an extension to `IHostBuilder` instead of a NServiceBus `Feature` implementation, unless it specifically alters the NServiceBus endpoint. Some existing ServiceControl features still use the `Feature` abstraction to register components. Migrate those that do not modify the NServiceBus endpoint to `IHostBuilder` extensions over time.
- Prefer `IHostedService` to NServiceBus `FeatureStartupTask`: ServiceControl hosts many background tasks. `IHostedService` and `FeatureStartupTask` are both abstractions for building background tasks. `FeatureStartupTask` implementations are tied to the lifecycle of an endpoint. `IHostedService` is tied to the lifecycle of the host application, and we prefer it in general. NOTE: ServiceControl starts `IHostedService` implementations in the order of registration, which gives more control over the startup sequence, and shuts them down in the reverse order. `FeatureStartupTask` implementations start in an order based on `Feature` activation, so controlling the startup sequence requires configuring feature dependencies. They also shut down in reverse order.
- Prefer `IServiceCollection` over `IConfigureComponents` and `IContainerBuilder`: Where possible, use the Microsoft DI abstraction (`IServiceCollection`) instead of the NServiceBus one (`IConfigureComponents`) or the Autofac one (`IContainerBuilder`).
  - When registering components from within an NServiceBus feature, using `IConfigureComponents` is still appropriate. Consider moving the code out of the NServiceBus feature. `IConfigureComponents` was [deprecated in NServiceBus version 8](https://github.com/Particular/NServiceBus/blob/335ed21dc7d230406d675bd61570b903a69c879c/src/NServiceBus.Core/obsoletes-v8.cs#L192).
  - When relying on Autofac-specific features, use `IContainerBuilder`. Prefer to implement features in a way that does not rely on Autofac-specific features. In the future, we may decide to remove our dependency on Autofac.
- Prefer `IServiceProvider` over `ILifetimeService` and `IContainer`: As above, use the Microsoft DI abstraction where possible and use `ILifetimeService` only where strictly necessary.
  - When relying on an Autofac-specific feature, use `ILifetimeService`.
  - Never use `IContainer`. It is functionally equivalent to `IServiceProvider` and was [deprecated in NServiceBus version 8](https://github.com/Particular/NServiceBus/blob/335ed21dc7d230406d675bd61570b903a69c879c/src/NServiceBus.Core/obsoletes-v8.cs#L252).

### Exceptions

Logging is the one exception to the preference for Microsoft abstractions. Use the NServiceBus logging abstractions instead of the Microsoft or NLog abstractions. The existing ServiceControl code uses the static `LogManager` classes to access the logging infrastructure, and all new code should follow this pattern for consistency. In the future we are likely to switch to the Microsoft abstraction.

## Prefer explicit container registration

Where possible, prefer explicit container registration for services over convention-based registration. Explicit registration shows which classes belong to which ServiceControl components or to which part of the ServiceControl infrastructure. It also controls which services are available within the container, which reduces inappropriate cross-component access. In the future we may be able to make this more explicit by moving ServiceControl components into their own assemblies and keeping non-shared services internal.

### Exceptions

A few things are still registered by convention. Autofac registers them, not the Microsoft DI abstractions.

- API controllers
- Scatter-gather API components

NServiceBus also scans types at startup and automatically registers any implementations of `Feature` and `IHandleMessage<>`. We leave this unchanged because turning it off would work against NServiceBus.


## Prefer explicit persistence operations

Use a direct data-store method when all inputs for a persistence operation fit in one method call. The method owns and disposes its EF Core scope or context or its RavenDB session, accepts a `CancellationToken`, and commits before returning. Returned entities are detached snapshots. Callers must not need to mutate tracked entities as an implicit persistence command.

Atomic operations that can encounter concurrency conflicts document their provider guarantees and translate expected provider exceptions into explicit domain outcomes. For example, a unique-key or optimistic-concurrency conflict should not escape when contention is part of the normal contract of the operation.

Use a specialized unit of work only when a caller composes several writes into one atomic batch. New unit-of-work APIs provide:

- an `I...UnitOfWorkFactory`
- a `StartNew` factory method
- a `Complete(CancellationToken)` commit method
- `IAsyncDisposable` lifetime ownership
- explicit operation-recording methods rather than mutation of tracked return values
- documented commit, abandon, repeated-completion, and concurrency behavior

Do not introduce generic `IDataSessionManager`-style abstractions or persistence managers with hidden call-order protocols. During review, prefer one explicit store operation unless caller-composed atomicity requires a unit of work.

## Avoid property injection

Avoid property injection, although the Autofac container can be configured to allow it. The Microsoft DI abstractions cannot specify property injection, and the default `IServiceProvider` implementation does not support it. Where possible, use constructor injection.

### Exceptions

A few places still use property injection. Autofac registers them, not the Microsoft DI abstractions.

- API controllers
- Scatter-gather API components
