# Coding and design guidelines

This document lists coding and design guidelines that should be followed when working on ServiceControl. In case of conflicts with other coding and design guidelines, this document should take precedence when working on ServiceControl.

## Prefer Microsoft abstractions

Microsoft maintains a number of abstractions for common cross-cutting concerns in the [framework libraries](https://docs.microsoft.com/en-us/dotnet/standard/framework-libraries) and in the `Microsoft.Extensions.*` packages. Where such an abstraction exists, we prefer to use it in preference to any other abstraction (including one of our own). 

This makes it easier for us to isolate the application from third party dependencies by relying on relatively stable abstractions. It also helps to keep parts of the application isolated from each other (e.g. running the embedded database in maintenance mode without starting the NServiceBus endpoint).

For example:

- **Prefer `IHostBuilder` extensions over NServiceBus features**: Unless a new feature specifically alters the NServiceBus endpoint, it should be added as an extension to `IHostBuilder` rather than as a NServiceBus `Feature` implementation. Some existing ServiceControl features may still be using the `Feature` abstraction to register components. If these features do not modify the NServiceBus endpoint, they should be migrated to `IHostBuilder` extensions over time.
- **Prefer `IHostedService` to NServiceBus `FeatureStartupTask`**: ServiceControl hosts many background tasks. `IHostedService` and `FeatureStartupTask` are both abstractions for building background tasks. `FeatureStartupTask` implementations are tied to the lifecycle of an endpoint. In general, we prefer to use the `IHostedService` abstraction which is tied to the lifecycle of the host application. NOTE: `IHostedService` implementations are started in the order that they are registered, which provides more control over the startup sequence. They are shut down in the reverse order. `FeatureStartupTask` implementations are started in an order that is based on the order of `Feature` activation. This means that controlling startup sequence has to be done by configuring feature dependencies. `FeatureStartupTask` implementations are also shut down in reverse order.
- **Prefer `IServiceCollection` over `IConfigureComponents` and `IContainerBuilder`**: Where possible, we use the Microsoft DI abstraction (`IServiceCollection`) rather than the NServiceBus one (`IConfigureComponents`) or the Autofac one (`IContainerBuilder`).
  - When registering components from within an NServiceBus Feature, it still makes sense to use `IConfigureComponents`, but we should consider whether it makes sense to move the code out of an NServiceBus feature. `IConfigureComponents` has been [deprecated in NServiceBus version 8](https://github.com/Particular/NServiceBus/blob/335ed21dc7d230406d675bd61570b903a69c879c/src/NServiceBus.Core/obsoletes-v8.cs#L192).
  - When relying on Autofac-specific features, it makes sense to use `IContainerBuilder`. We prefer to implement features in way that does not rely on Autofac-specific features. In the future, we may decide to remove our dependency on Autofac.
- **Prefer `IServiceProvider` over `ILifetimeService` and `IContainer`**: As above, we prefer to use the Microsoft DI abstraction where possible and only fall back `ILifetimeService` where strictly necessary.
  - When relying on a Autofac-specific feature, `ILifetimeService` should be used.
  - `IContainer` should never be used. It is functionally equivalent to `IServiceProvider` and has been [deprecated in NServiceBus version 8](https://github.com/Particular/NServiceBus/blob/335ed21dc7d230406d675bd61570b903a69c879c/src/NServiceBus.Core/obsoletes-v8.cs#L252).

### Exceptions

There is one exception to the preference for Microsoft abstractions

- **Use NServiceBus Logging abstractions over Microsoft or NLog abstractions**: The existing ServiceControl code uses the static `LogManager` classes to get access to the logging infrastructure and all new code should follow this pattern. In the future we are likely to switch to the Microsoft abstraction but until then we want to maintain consistency.


## Prefer explicit container registration

Where possible, we prefer explicit container registration for services instead of convention-based registration. This provides better visibility of which classes belong to which ServiceControl components or to which part of the ServiceControl infrastructure. It also gives us more explicit control over which services are available within the container, which helps to reduce inappropriate cross-component access. In the future we may be able to make this more explicit by moving ServiceControl components into their own assemblies and keeping non-shared services internal.

### Exceptions

There are a few things that are still registered using convention. Note that these are all registered using Autofac and not the Microsoft DI abstractions.

- **API Controllers**
- **Scatter-Gather API components**

Additionally, because NServiceBus does a type-scan at startup it will automatically register any implementations of `Feature` and `IHandleMessage<>`. We have chosen to leave this alone as we would be fighting with NServiceBus in order to turn this off. 


## Avoid property injection

Although the Autofac container can be configured to allow property injection, we prefer to avoid it. There is no way to specify property injection using the Microsoft DI abstractions, and the default `IServiceProvider` implementation does not support it. Where possible, use constructor injection instead.

### Exceptions

There are a few places where property injection is still used. These are all registered using Autofac, and not the Microsoft DI abstractions.

- **API Controllers**
- **Scatter-Gather API components**
