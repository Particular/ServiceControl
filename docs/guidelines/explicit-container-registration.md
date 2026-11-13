# Prefer explicit container registration

Applies to: any change under `src/**` except `src/ServiceControl.Config/**` that adds or changes a service registration or a constructor dependency.

## Rules

- Services must be registered explicitly on `IServiceCollection`, not by scanning assemblies for conventions.
- Components must receive dependencies through constructors, not through property injection, which the Microsoft container does not support.
- Leave registrations that a framework owns as they are: ASP.NET Core registers controllers, and NServiceBus registers `Feature` and `IHandleMessages<>` implementations.

## Rationale

Explicit registration shows which classes belong to which ServiceControl component or to which part of the infrastructure. It also controls which services the container offers, which reduces inappropriate access across components.
