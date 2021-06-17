This document lists identified coding and design guidelines that should be followed when working on ServiceControl. In case of conflicts with other coding and design guidelines, this document should take precedence when working on ServiceControl.

## Prefer Microsoft abstractions

Microsoft maintains a number of abstractions for common cross-cutting concerns in the .NET common library and in the `Microsoft.Extensions.*` namespaces. Where such an abstraction exists, we prefer to use it over an abstraction provided by a different assembly (including one of our own). 

This makes it easier for us to isolate the application from 3rd party dependencies by relying on fairly stable abstractions. It also helps to keep parts of the application isolated from each other (i.e. being able to run the embedded database for maintenance mode without starting the NServiceBus endpoint).

For example:

- **Prefer IHostBuilder extensions over NServiceBus features**: Unless the code being added is specifically altering the NServiceBus endpoint, it makes sense to add new ServiceControl code directly as extensions to `IHostBuilder` rather than as NServiceBus `Feature` implementations. Some existing ServiceControl features may still be using the `Feature` abstraction to register its components. If these features are not modifying the NServiceBus endpoint, then they should be migrated to `IHostBuilder` extensions over time.
- **Prefer Hosted Services over NServiceBus FeatureStartupTasks**: ServiceControl includes a lot of background tasks and these are both abstractions for building background tasks. `FeatureStartupTask` implementations are tied to the lifecycle of an endpoint. In general, we prefer to use the `IHostedService` abstraction which is tied to the lifecycle of the host application. NOTE: Hosted Services are started in the order that they are registered which allows us to better control the startup sequence. They are shut down in the reverse order.
- **Prefer IServiceCollection over IConfigureComponents or IContainerBuilder**: Where possible we'd rather use the generic Microsoft DI abstractions over the NServiceBus one (IConfigureComponents) or the Autofac one (IContainerBuilder).
  - When registering components from within an NServiceBus Feature, it still  makes sense to use IConfigureComponents but we should consider whether it makes sense to move the code out of NServiceBus features
  - When relying on Autofac specific features, it makes sense to rely on IContainerBuilder. If a feature can be implemented in a way to avoid this, it should be preferred. We may in the future decide to remove our dependency on Autofac but that decision has not been made.
- **Prefer IServiceProvider over ILifetimeService over IContainer**: As above, we'd rather rely on the generic Microsoft DI abstraction where possible and only fall back to NServiceBus or Autofac abstractions where strictly necessary.
  - If relying on a specific feature of Autofac, then `ILifetimeService` should be used.
  - `IContainer` should never be used. It is functionally equivalent to `IServiceProvider`

### Exceptions

There is one exception to the preference for Microsoft abstractions

- **Use NServiceBus Logging abstractions over Microsoft or NLog abstractions**: The existing ServiceControl code uses the static `LogManager` classes to get access to the logging infrastructure and all new code should follow this pattern. In the future we are likely to switch to the Microsoft abstraction but until then we want to maintain consistency.


## Prefer explicit container registration

Where possible, we prefer explicit container registration for services as opposed to convention-bassed registration. This allows us to better see which classes belong to which ServiceControl components or which part of the ServiceControl infrastructure. It also allows us more explicit control over what services are available within the container which helps to reduce inappropriate cross-component access. In the future we may be able to make this more explicit by moving ServiceControl components into their own assemblies and keeping non-shared services internal.

### Exceptions

There are a few things that are still registered via convention. Note that these are all registered via Autofac and not via the Microsoft DI abstractions.

- **API Controllers**
- **Scatter-Gather API components**

Additionally, because NServiceBus does a type-scan at startup it will automatically register any implementations of `Feature` and `IHandleMessage<>`. We have chosen to leave this alone as we would be fighting with NServiceBus in order to turn this off. 


## Avoid property injection

Although the Autofac container can be configured to allow property injection, we prefer to avoid it. There is no way to specify property injection using the generic microsoft DI abstractions, nor does the default Service Provider support it. Where possible, use constructor injection instead.

### Exceptions

There are a few places where property injection is still used. Note that these are all registered via Autofac and not via the Microsoft DI abstractions.

- **API Controllers**
- **Scatter-Gather API components**
