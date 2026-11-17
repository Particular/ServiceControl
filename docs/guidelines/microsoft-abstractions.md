# Prefer Microsoft abstractions

Applies to: every change under `src/**` except `src/ServiceControl.Config/**` that adds or changes a hosted service, a host or builder extension, a service registration, or logging.

## Rules

- Code must use the abstraction from the .NET framework libraries or `Microsoft.Extensions.*` when one exists, instead of another abstraction, including one of our own.
- A new capability must be an extension on `IHostApplicationBuilder`, not an NServiceBus `Feature`, unless it specifically changes the NServiceBus endpoint.
- Background work must be an `IHostedService`, not an NServiceBus `FeatureStartupTask`, unless it must follow the endpoint's lifecycle.
- A hosted service that depends on startup order must be registered in that order, because the host starts hosted services in registration order and stops them in reverse.
- Services must be registered on `IServiceCollection` and resolved through `IServiceProvider`.
- New code should log through a constructor-injected `ILogger<T>`, not `NServiceBus.Logging`, as the [NServiceBus logging guidance](https://github.com/Particular/NServiceBus/blob/master/docs/logging.md) prefers.

## Rationale

Stable abstractions isolate the application from third-party dependencies and keep parts of it isolated from each other. For example, the embedded database can run in maintenance mode without starting the NServiceBus endpoint.
