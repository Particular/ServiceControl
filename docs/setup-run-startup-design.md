# Setup / Run startup design

Status: **proposal** — analysis of the current Run/Setup architecture and a plan to make "setup then run" a single-process operation.

Related: [Handling unavailable runtime dependencies](handling-unavailable-runtime-dependencies.md), [Coding and design guidelines](coding-and-design-guidelines.md), GitHub issue [#4392](https://github.com/Particular/ServiceControl/issues/4392).

---

## 1. Problem

ServiceControl has always had two modes:

* **Setup** — create queues, open the database, run migrations if this is an upgrade.
* **Run** — host the endpoint and the API.

On a Windows host this is fine: the installer runs `--setup` once, then the service runs. Containers remove that seam. A container image is started, not installed, so requiring a separate setup step forces an init container that must be outfitted with **exactly the same settings** as the primary container — a duplicated configuration surface that drifts and fails confusingly.

The current answer is a compromise: `--setup-and-run` **forks a second copy of the executable** to perform setup, then the "prime" process continues into run mode. This works, but it is a hack, and it was introduced as a scope-limiting measure precisely because the startup logic was not shaped to allow setup and run to share a process without leaving "unhappy surprises" scattered through the codebase.

This document describes how the current logic is organized, why the hack was necessary, what the target design is, the risks, and the order of work to deliver it.

---

## 2. Current state

### 2.1 The hack runs before argument parsing

`src/ServiceControl/Program.cs` (and near-identical copies in `ServiceControl.Audit` and `ServiceControl.Monitoring`):

```csharp
ExeConfiguration.PopulateAppSettings(Assembly.GetExecutingAssembly());
var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace);
LoggingConfigurator.ConfigureLogging(loggingSettings);
logger = LoggerUtil.CreateStaticLogger(typeof(Program));
AppDomain.CurrentDomain.UnhandledException += ...;

// Hack: See https://github.com/Particular/ServiceControl/issues/4392
var exitCode = await IntegratedSetup.Run();
if (exitCode != 0) { return exitCode; }

var arguments = new HostArguments(args);
if (arguments.Help) { arguments.PrintUsage(); return 0; }

var settings = new Settings(loggingSettings: loggingSettings);
await new CommandRunner(arguments.Command).Execute(arguments, settings);
return 0;
```

`IntegratedSetup.Run()` (`src/ServiceControl.Infrastructure/IntegratedSetup.cs`) does the following:

1. Reads `Environment.GetCommandLineArgs()` (not `args`, because it wants the entry-assembly path).
2. If `--setup-and-run` is absent, returns `0` immediately — no-op.
3. Rewrites every `--setup-and-run` token in place to `--setup`.
4. Removes `argv[0]` unless the process is `dotnet`.
5. `Process.Start`s a child with stdout/stderr redirected into the parent's streams.
6. Returns the child's exit code; a non-zero code aborts the parent before it ever reaches run mode.

**The parent survives its own `--setup-and-run` argument by accident.** That token is registered in *no* `OptionSet` anywhere in the repository — the only occurrences are `IntegratedSetup` itself, the three `launchSettings.json` files, the three `Container-README.md` files, and `src/container-integration-test/servicecontrol.yml`. NDesk.Options routes unknown options into its `unprocessed` list rather than throwing, so `HostArguments` leaves `Command = typeof(RunCommand)` (`src/ServiceControl/Hosting/HostArguments.cs:104`) and the parent proceeds normally.

So `--setup-and-run` is not a mode. It means *"fork a setup process, then run normally."*

### 2.2 Three parallel command stacks

Each application independently carries a complete copy of the command machinery:

| | ServiceControl | ServiceControl.Audit | ServiceControl.Monitoring |
|---|---|---|---|
| Command directory | `Hosting/Commands/` | `Infrastructure/Hosting/Commands/` | `Hosting/Commands/` |
| `AbstractCommand` | yes | yes | yes |
| `CommandRunner(Type)` via `Activator.CreateInstance` | yes | yes | yes |
| `HostArguments` | yes | yes | yes |
| Vendored `Options.cs` (NDesk) | yes (~line 605) | yes | yes |
| `SetupCommand` / `RunCommand` | yes / yes | yes / yes | yes / yes |
| Extra commands | `MaintenanceMode`, `ImportFailedErrors`, `ErrorIngestionOnly` | `MaintenanceMode`, `ImportFailedAudits` | — |

Three copies of the option parser, three copies of the dispatch loop, three subtly different argument-precedence chains.

`HostArguments` resolves the command through a chain of `OptionSet`s with early returns:

```
externalInstallerOptions   --setup / -s            → SetupCommand        (+ --skip-queue-creation)
maintenanceOptions         -m / --maintenance      → MaintenanceModeCommand
reimportFailedErrors       --import-failed-errors  → ImportFailedErrorsCommand
errorIngestionOnly         --error-ingestion-only   → ErrorIngestionOnlyCommand
defaultOptions             -? / -h / --help        → Help
```

plus a settings-driven override: if `MaintenanceMode` is `true` in configuration, `-m` is appended to the argument list before parsing.

### 2.3 Two incompatible host shapes

This is the core of the problem:

| | Setup | Run |
|---|---|---|
| Builder | `Host.CreateApplicationBuilder()` | `WebApplication.CreateBuilder()` |
| Persistence registration | `AddServiceControlInstallers` → `IPersistence.AddInstaller` | `AddServiceControl` → `IPersistence.AddPersistence` |
| NServiceBus endpoint | not created | `AddNServiceBusEndpoint(configuration)` |
| Web / API | none | `AddServiceControlApi`, authentication, HTTPS, integrated ServicePulse |
| Lifetime | `StartAsync` → work → `StopAsync` | `RunAsync(rootUrl)` |

`SetupCommand` in the primary app:

```csharp
var hostBuilder = Host.CreateApplicationBuilder();
hostBuilder.AddServiceControlInstallers(settings);

var componentSetupContext = new ComponentInstallationContext();
foreach (ServiceControlComponent component in ServiceControlMainInstance.Components)
{
    component.Setup(settings, componentSetupContext, hostBuilder);
}

using IHost host = hostBuilder.Build();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) EventSourceCreator.Create();

await host.StartAsync(cancellationToken);

if (args.SkipQueueCreation) { /* log "Skipping queue creation" */ }
else
{
    var transportSettings = settings.ToTransportSettings(componentSetupContext);
    transportSettings.RunCustomChecks = false;
    var transportCustomization = TransportFactory.Create(transportSettings);
    await transportCustomization.ProvisionQueues(transportSettings, componentSetupContext.Queues, cancellationToken);
}

await using (var scope = host.Services.CreateAsyncScope())
{
    if (scope.ServiceProvider.GetService<IDatabaseMigrator>() is { } databaseMigrator)
        await databaseMigrator.ApplyMigrations(cancellationToken);
    if (scope.ServiceProvider.GetService<IBodyStorageInstaller>() is { } bodyStorageInstaller)
        await bodyStorageInstaller.Provision(cancellationToken);
}

await host.StopAsync(cancellationToken);
```

### 2.4 Why the two shapes cannot share a container

`IPersistence` exposes two registration methods:

```csharp
public interface IPersistence
{
    void AddPersistence(IServiceCollection services);
    void AddInstaller(IServiceCollection services);
}
```

In `src/ServiceControl.Persistence.RavenDB/RavenPersistence.cs` **both** call the same private `ConfigureLifecycle(services)`, which contains:

```csharp
services.AddHostedService<RavenPersistenceLifecycleHostedService>();   // additive — not TryAdd
```

`AddHostedService` is additive. Register both paths in one container and the hosted service is registered twice, so `EmbeddedDatabase.Start` is called twice.

EF Core has the same shape: `AddInstaller` and `AddPersistence` both perform `RegisterSettings` + `ConfigureDbContext`, so running both means `AddDbContext` twice.

The two registration sets **intersect without being nested**. That is the real reason `SetupCommand` cannot simply be followed by `RunCommand` — it is not inertia, the registrations genuinely collide.

A second landmine points the same way: `PersistenceFactory.Create(settings, maintenanceMode)` **mutates shared state** — it sets `settings.PersisterSpecificSettings.MaintenanceMode` and `.RunRetentionSweep = !settings.ErrorIngestionOnly` (`src/ServiceControl/Persistence/PersistenceFactory.cs:20-21`). Today `AddServiceControl` and `AddServiceControlInstallers` each call it. In separate processes that is harmless; in one process, two calls with different modes would fight over the same settings object.

### 2.5 Persistence loading is a second duplication axis

Audit did not merely copy the command stack — it evolved a parallel, incompatible persistence abstraction:

| | ServiceControl (primary) | ServiceControl.Audit |
|---|---|---|
| Factory | `PersistenceFactory.Create(settings, maintenanceMode)` | `PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings)` + `.BuildPersistenceSettings(settings)` |
| Installer registration | `IPersistence.AddInstaller(services)` | `services.AddInstaller(persistenceSettings, persistenceConfiguration)` |
| Run registration | `IPersistence.AddPersistence(services)` | `services.AddPersistence(settings, persistenceConfiguration)` |

Two different extension-method shapes solving the same problem. This is the substance of issue #4392 observation #1 — "none of them does it quite the same" applies one layer deeper than the commands, and unifying it is a larger job than the command stack alone.

### 2.6 What "setup" actually does — the complete inventory

Across all three applications, setup consists of exactly five things:

| # | Operation | Mechanism |
|---|---|---|
| 1 | Initialize persistence lifecycle | `RavenEmbeddedPersistenceLifecycle` → `EmbeddedDatabase.Start` + `database.Connect` retry loop (`DatabaseLoadTimeoutException`, 500 ms) + `DatabaseSetup.Execute` |
| 2 | Provision transport queues | `ITransportCustomization.ProvisionQueues` — throwaway transport: `Initialize` → `Shutdown` |
| 2b | Create pub/sub topics | `ASBSTransportCustomization.ProvisionQueues` override — one topic per `EventTypesPublished` via the management client, swallowing `MessagingEntityAlreadyExists` / transient errors |
| 3 | Apply schema migrations | `IDatabaseMigrator.ApplyMigrations` → raise `MigrationCommandTimeout` → `RequireSchema` → `MigrateAsync` |
| 4 | Provision body storage | `IBodyStorageInstaller.Provision` |
| 5 | Create Windows EventSource | `EventSourceCreator.Create()` (Windows only) |

Every one of these is **idempotent**. None requires the endpoint. None requires the web layer. They are a *prefix of startup*, not a different application.

Worth noting for #3: `RequireSchema` deliberately throws `InvalidOperationException` when a configured schema is absent — "ServiceControl does not create schemas, the same way it does not create the database… run setup again." That is a deliberate, documented failure, and it must keep failing the same way.

### 2.7 What `Component.Setup` actually does

```csharp
abstract class ServiceControlComponent
{
    public abstract void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder);
    public virtual void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder) { }
}
```

All five overrides were inspected. **Every one touches only `context`. Not one uses the `hostBuilder` parameter.** They are pure declarations into `ComponentInstallationContext`:

| Component | Declares |
|---|---|
| `HostingComponent` | queue: `settings.InstanceName` |
| `RecoverabilityComponent` | queues: staging, error (if `IngestErrorMessages`), error log (if forwarding); events: `FailedMessagesArchived`, `FailedMessagesUnArchived`, `MessageFailed`, `MessageFailureResolvedByRetry`, `MessageFailureResolvedManually`, `MessageEditedAndRetried` |
| `LicensingComponent` | queue: `ServiceControlThroughputDataQueue` |
| `HeartbeatMonitoringComponent` | events: `HeartbeatRestored`, `HeartbeatStopped` (if integrations publishing enabled) |
| `CustomChecksComponent` | events: `CustomCheckFailed`, `CustomCheckSucceeded` (if integrations publishing enabled) |

**Crucially, the run path already calls `Setup`.** `src/ServiceControl/HostApplicationBuilderExtensions.cs:60-65`:

```csharp
var componentSetupContext = new ComponentInstallationContext();
var serviceControlComponents = components is { Length: 0 } ? ServiceControlMainInstance.Components : components;
foreach (ServiceControlComponent component in serviceControlComponents)
{
    component.Setup(settings, componentSetupContext, hostBuilder);
}
```

The run path already knows exactly which queues and topics must exist. It simply never acts on that knowledge. **The declaration is already in the run pipeline; only the action is missing.** This is the strongest single indicator that single-process setup-and-run is achievable without a rewrite.

### 2.8 Ordering today: constraints expressed as registration position

The system has ordering requirements in **both** directions, and today both are expressed purely by where the `AddHostedService` call is placed:

**Must happen *before* the endpoint receives** — all setup work. Guaranteed today only by "different process."

**Must happen *after* the endpoint is up** — `src/ServiceControl.Audit/HostApplicationBuilderExtensions.cs:76-80`:

```csharp
// Configure after the NServiceBus hosted service to ensure NServiceBus is already started
if (settings.IngestAuditMessages)
{
    services.AddHostedService<AuditIngestion>();
}
```

**Transitive case** — the `Lazy<IMessageDispatcher>` workaround, duplicated in both apps with a comment that states the problem outright (`HostApplicationBuilderExtensions.cs:99-103`):

```csharp
// Core registers the message dispatcher to be resolved from the transport seam. The dispatcher
// is only available though after the NServiceBus hosted service has started. Any hosted service
// or component injected into a hosted service can only depend on this lazy instead of the dispatcher
// directly and to make things more complex of course the order of registration still matters ;)
services.AddSingleton(provider => new Lazy<IMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>));
```

### 2.9 Setup ordering is inconsistent across the three apps

| App | Order of operations |
|---|---|
| **ServiceControl** | build host → `StartAsync` (DB opens) → **then** provision queues → migrations → body storage → `StopAsync` |
| **ServiceControl.Audit** | provision queues **first** (if `IngestAuditMessages`) → then build host → `StartAsync` → `StopAsync` |
| **ServiceControl.Monitoring** | no host at all — just `ProvisionQueues(transportSettings, [], ct)` |

Three orderings, none documented as deliberate.

---

## 3. The three obstacles

Everything above reduces to three things that must change. These map one-to-one onto the observations in issue #4392.

**Obstacle A — intersecting registration sets.** `AddInstaller ∩ AddPersistence ≠ ∅`, and the intersection registers additive hosted services. *Fix: make one a strict subset of the other.*

**Obstacle B — no ordering primitive.** Hosted services start in registration order; the codebase already complains about this in its own comments. Setup must finish before the endpoint's receiver polls a queue that may not exist yet. Today that is guaranteed only by process separation. *Fix: adopt an explicit lifecycle phase.*

**Obstacle C — `Setup` vs `Configure` is a naming convention, not a contract.** Nothing stops a `Setup` override from doing DI work, and nothing specifies when it runs relative to other components' `Setup`. *Fix: make the declarative form structurally incapable of side effects.*

---

## 4. Target design

> **Principle: setup is not a different application. It is a prefix of the same one.**

### 4.1 Collapse `AddInstaller` into `AddPersistence`

Make persistence registration a single **superset** method. `IDatabaseMigrator` and `IBodyStorageInstaller` are inert services in run mode — harmless to register and simply unused unless a setup step resolves them.

```csharp
public interface IPersistence
{
    void AddPersistence(IServiceCollection services, PersistenceMode mode);
    // AddInstaller deleted
}
```

`ConfigureLifecycle` stays private and is called **exactly once**. The new `AddPersistence` = old `AddPersistence` ∪ old `AddInstaller`. This single change removes the collision that forced the child process.

Corollary: `PersistenceFactory.Create` must be called **once per host**, with the mode decided up front rather than mutated by whichever registration method happens to run.

### 4.2 Make the composition root a strict prefix chain

Split `AddServiceControl` so the setup subset is *structurally* a prefix rather than aspirationally one:

```csharp
// THE PREFIX — everything setup needs. Creates no endpoint, binds no port, receives no messages.
public static void AddServiceControlSetup(
    this IHostApplicationBuilder hostBuilder,
    Settings settings,
    StartupPlan plan,
    params ReadOnlySpan<ServiceControlComponent> components)
{
    hostBuilder.Logging.ClearProviders();
    hostBuilder.Logging.ConfigureLogging(settings.LoggingSettings.LogLevel);

    var declaration = new ComponentSetupDeclaration();
    foreach (var component in components) component.DeclareRequirements(settings, declaration);

    var transportSettings = settings.ToTransportSettings(declaration);
    var transportCustomization = TransportFactory.Create(transportSettings);
    transportCustomization.AddTransportForPrimary(hostBuilder.Services, transportSettings);

    hostBuilder.Services.AddSingleton(declaration);
    hostBuilder.Services.AddSingleton(settings);
    hostBuilder.Services.TryAddSingleton(TimeProvider.System);
    hostBuilder.Services.AddPersistence(settings, plan.PersistenceMode);   // ← exactly once
    hostBuilder.Services.AddSetupPlan(plan);                               // ← ordered steps
}

// THE SUFFIX — starts with the prefix, then adds the endpoint, web layer and background work.
public static void AddServiceControl(
    this IHostApplicationBuilder hostBuilder,
    Settings settings,
    EndpointConfiguration configuration,
    params ReadOnlySpan<ServiceControlComponent> components)
{
    hostBuilder.AddServiceControlSetup(settings, StartupPlan.ForRun(settings), components);

    // ... license check, NServiceBusFactory.Configure, AddNServiceBusEndpoint,
    //     email notifications, async timers, custom checks, Windows service, components
}
```

`--setup` uses the prefix and nothing else. Run modes use the suffix. Same registrations, one code path, no intersection problem — because one is literally the first N lines of the other.

The split point is clean and easy to audit: there are exactly **three** `AddNServiceBusEndpoint` call sites in the entire source tree — `ServiceControl/HostApplicationBuilderExtensions.cs:130`, `ServiceControl.Audit/HostApplicationBuilderExtensions.cs:72`, `ServiceControl.Monitoring/HostApplicationBuilderExtensions.cs:76`. The prefix is everything above one greppable line per app.

### 4.3 Setup as ordered steps, executed in `BeforeStartAsync`

Replace the inline work in `SetupCommand` with resolvable, ordered steps:

```csharp
public interface ISetupStep
{
    int Order { get; }              // explicit, documented, greppable
    string Name { get; }
    Task Run(CancellationToken cancellationToken);
}
```

Executed by a single orchestrator using `IHostedLifecycleService`:

```csharp
sealed class SetupPipelineHostedService(StartupPlan plan, IServiceProvider services) : IHostedLifecycleService
{
    public async Task BeforeStartAsync(CancellationToken cancellationToken)
    {
        foreach (var step in plan.Steps)   // ordered by Order
        {
            logger.LogInformation("Setup: {Step} starting", step.Name);
            await step.Run(cancellationToken);
            logger.LogInformation("Setup: {Step} complete", step.Name);
        }
    }

    // StartAsync / AfterStartedAsync / StoppingAsync / StoppedAsync: no-op
}
```

**Why this is the key move.** The .NET host runs `BeforeStartAsync` as a complete pass over *all* hosted services before it runs *any* `StartAsync`. Setup completing before the NServiceBus endpoint starts receiving becomes a **hard guarantee provided by the host**, not a registration-order hope. It retires the `;)` comment at `HostApplicationBuilderExtensions.cs:102`.

Note that persistence initialization becomes **step 100 inside the same pipeline** rather than a separate hosted service. `RavenPersistenceLifecycleHostedService` then shrinks to shutdown-only (`StopAsync`), with the existing `SemaphoreSlim(1,1)` in `RavenEmbeddedPersistenceLifecycle` retained as a backstop. One initializer, one stopper.

Steps are thin wrappers over what `SetupCommand` already does:

```csharp
sealed class ProvisionQueuesStep(
    ITransportCustomization transport,
    TransportSettings transportSettings,
    ComponentSetupDeclaration declaration,
    Settings settings) : ISetupStep
{
    public int Order => 200;
    public string Name => "Provision transport queues";

    public Task Run(CancellationToken cancellationToken) => settings.SkipQueueCreation
        ? Task.CompletedTask   // log "Skipping queue creation"
        : transport.ProvisionQueues(transportSettings, declaration.Queues, cancellationToken);
}

sealed class ApplyMigrationsStep(IServiceScopeFactory scopeFactory) : ISetupStep
{
    public int Order => 300;
    public string Name => "Apply database migrations";

    public async Task Run(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<IDatabaseMigrator>() is { } migrator)
            await migrator.ApplyMigrations(cancellationToken);
    }
}
```

### 4.4 `StartupPlan` — one place that says what each mode does

```csharp
StartupPlan.SetupOnly      → [Persistence, Queues, Migrations, BodyStorage]
StartupPlan.SetupAndRun    → [Persistence, Queues, Migrations, BodyStorage]   // identical by design
StartupPlan.RunOnly        → []
StartupPlan.Maintenance   → [Persistence]                       (PersistenceMode = Maintenance)
StartupPlan.ImportFailed  → [Persistence]
```

Maintenance mode and `--import-failed-errors` fall out of the same mechanism — they are just shorter plans. `StartupModeTests` no longer needs its "ideally we'd use `MaintenanceModeCommand` but that indefinitely blocks due to `RunAsync` not terminating" workaround.

### 4.5 Phase diagram

```
BeforeStartAsync pass          ← setup lives here, ordered by ISetupStep.Order
  100  PersistenceLifecycle.Initialize   (open DB / start embedded server)
  200  ProvisionQueues                  (queues + ASB topics)
  300  ApplyMigrations                 (requires 100)
  400  ProvisionBodyStorage             (requires 100)
  500  EventSourceCreator.Create        (Windows only)
──────────────────────────────────────────────────────────────────────
StartAsync pass                ← consumers. DB open, queues exist, schema current.
  NServiceBus endpoint  →  receivers begin polling
  custom checks, async timers, throughput reporting, ...
──────────────────────────────────────────────────────────────────────
AfterStartedAsync pass         ← things that need a live endpoint
  AuditIngestion (replaces the "configure after the NServiceBus hosted service" comment)
```

`AfterStartedAsync` is the mirror-image win: `AuditIngestion`'s "must start after the endpoint" constraint stops being a registration-position convention and becomes a named phase. Both directions get a primitive instead of relying on call order.

### 4.6 Make component setup structurally declarative

Since no override uses `hostBuilder`, delete the parameter and rename to make intent unmissable:

```csharp
abstract class ServiceControlComponent
{
    // DI registration. Runs once, during composition.
    public abstract void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder);

    // Declares what this component needs to EXIST before the app can run.
    // No host builder, no service collection, no I/O — structurally incapable of doing work.
    public virtual void DeclareRequirements(Settings settings, IComponentSetupDeclaration declaration) { }
}

public interface IComponentSetupDeclaration
{
    void CreateQueue(string queueName);
    void PublishesEvent<TEvent>();
}
```

This answers observation #3 directly. A component physically cannot perform an un-ordered side effect during setup, because it is not handed anything capable of performing one.

### 4.7 Make `--setup-and-run` a real argument

```csharp
var setupAndRunOptions = new OptionSet
{
    { "setup-and-run", "Run setup, then run normally (container entrypoint).",
      _ => { Command = typeof(RunCommand); RunSetup = true; } }
};
```

`IntegratedSetup.cs` is deleted outright. `Program.cs` loses its first three executable lines.

### 4.8 Alignment with existing guidelines

This design is not fighting the house style — it is an application of it.

* **"Prefer Microsoft abstractions"** (`coding-and-design-guidelines.md:7`). `IHostedLifecycleService` is a Microsoft abstraction. That same section states the goal almost verbatim: *"It also helps to keep parts of the application isolated from each other (e.g. running the embedded database in maintenance mode without starting the NServiceBus endpoint)."* That is exactly the axis this design makes explicit via `StartupPlan`.
* **"Prefer `IHostedService` to NServiceBus `FeatureStartupTask`"** (`:14`) notes that *"implementations are started in the order that they are registered, which provides more control over the startup sequence."* `IHostedLifecycleService` is the same family and provides *more* control — phase plus order, rather than position alone.
* **"Prefer explicit container registration"** (`:29`). `StartupPlan` is an explicit, reviewable list of what each mode does, rather than a convention.
* **Failure model** (`handling-unavailable-runtime-dependencies.md`): *"Failures detected during startup → instance stops immediately."* Setup in `BeforeStartAsync` *is* startup — a failed migration aborts the host before anything is listening, which is precisely the documented contract.

---

## 5. External contracts that must be preserved

| Contract | Consumer | How it is preserved |
|---|---|---|
| `{exe} --setup [--skip-queue-creation]`, `INSTANCE_NAME` env var, non-zero exit on failure, diagnostics on stderr. `ReadToEnd()` then `WaitForExit()` — "we will wait 'forever'" | `src/ServiceControlInstaller.Engine/Setup/InstanceSetup.cs` (Windows installer) | `--setup` keeps working via the prefix host. **Must** ensure a `StartAsync` failure surfaces as a clean non-zero exit with a readable stderr message, not a swallowed `HostStartException` trace. |
| `--setup-and-run` | 3× `launchSettings.json`, 3× `Container-README.md`, `src/container-integration-test/servicecontrol.yml` | Becomes a real `OptionSet` entry. Same string, same observable effect. |
| `new SetupCommand().Execute(new HostArguments([]), settings)` | `src/ServiceControl.AcceptanceTests/TestSupport/ServiceControlComponentRunner.cs` | Keep `SetupCommand` as a thin facade over the prefix host. |
| `ImportFailedErrorsCommand.BuildHost(settings)`, `AddPersistence(settings, maintenanceMode: true)` | `src/ServiceControl.AcceptanceTests.RavenDB/StartupModeTests.cs` | Becomes `StartupPlan.ImportFailed` / `.Maintenance`. Update call sites in the same change. |
| Container `HEALTHCHECK --start-period=10s` against `/health` | `src/ServiceControl/Dockerfile` | Unchanged. The port still is not bound until setup completes, so the health check still gates correctly on setup. |
| Auto-migration on every container start | Container deployments | Already the behavior today (`--setup-and-run` runs `ApplyMigrations`). Preserved. If the team ever wants migration opt-in, `StartupPlan` is the single place to express it. |

---

## 6. Benefits

Beyond "the hack is gone":

* **Halves the container's startup footprint.** Today setup forks a second CLR, re-reads every assembly, re-runs the manifest / `AssemblyLoadContext` scan, and re-parses all settings — then exits.
* **Log correlation.** The child's stdout/stderr is currently piped through the parent with no ordering guarantee under load and duplicated formatting. In-process, logs are ordered and single-prefixed.
* **One composition root.** `--setup`, `--setup-and-run`, `--run`, `--maintenance`, `--import-failed-errors`, `--error-ingestion-only` and the acceptance-test harness all draw from the same prefix. "Works in setup but not in run" becomes structurally impossible.
* **Ordering becomes a guarantee rather than a convention.** Both directions — setup-before-endpoint and `AuditIngestion`-after-endpoint — get named phases instead of registration positions and `Lazy<>` workarounds.
* **Removes a duplicated configuration surface.** No init container, therefore no second copy of settings to keep in sync.
* **Aligns with the documented failure model** rather than working around it.

---

## 7. Risks and mitigations

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | **Persistence unification (§4.1) is the genuinely dangerous change.** Two registration paths becoming one means any hidden asymmetry between `AddInstaller` and `AddPersistence` becomes a live bug — most acutely a double `EmbeddedDatabase.Start` or double `AddDbContext`. | **High** | Land as its own PR, alone. Add a DI-registration test asserting **exactly one** lifecycle hosted service of each type in the unified container, for both RavenDB and EF Core. Do not bundle with any other change. |
| R2 | **`PersistenceFactory` shared-state mutation.** Two calls with different `maintenanceMode` in one process would fight over `settings.PersisterSpecificSettings`. | High | Enforce single-call-per-host by construction: the mode is resolved once in `StartupPlan` and the factory is not reachable from the registration methods afterwards. Add an assertion that fails on a second call. |
| R3 | **Setup-before-receive ordering regresses.** If a step lands outside `BeforeStartAsync`, the endpoint can poll a queue that does not exist yet, producing a critical error on fresh installs. | High | Add an explicit ordering test: on a clean environment, assert the queue-provisioning log line precedes the first receive. The `BeforeStartAsync` guarantee is the mechanism; the test is the guard against someone moving it. |
| R4 | **Windows installer contract breakage.** `InstanceSetup` reads stderr then waits forever and keys off the exit code. A changed failure path could hang the installer or report success on failure. | High | Explicit test: force a migration failure (e.g. missing schema — `RequireSchema` already throws deliberately) and assert non-zero exit + readable stderr + process termination. |
| R5 | **`IHostedLifecycleService` is new to this codebase.** Introducing an unfamiliar pattern risks inconsistent later use. | Medium | Introduce it in exactly one place (the orchestrator), with a comment explaining the three-pass guarantee, so it does not become a second ad-hoc pattern. |
| R6 | **Audit's persistence abstraction is a separate, larger unification.** Tempting to do at the same time; would make the risky change much riskier. | Medium | Explicitly sequenced *after* the primary app is proven (Phase 8). Do not design both simultaneously. |
| R7 | **Behavior drift in `ProvisionQueues`.** It currently creates a throwaway transport (`Initialize` → `Shutdown`). In a single host the transport is initialized twice. | Low | Acceptable — this is what setup already does, and `HostSettings` is explicitly built for it (`runImmediately: true`, `null` hosted service: *"not hosted by core, transport SHOULD adjust accordingly"*). Keep the throwaway rather than coupling provisioning to the endpoint lifecycle. |
| R8 | **Step ordering becomes a new implicit dependency.** `Order` values could be set carelessly. | Low | Values are explicit, spaced by 100, and declared in one file (`StartupPlan`) so the whole sequence is reviewable in one screen. |
| R9 | **Setup runs on every container start and is slow.** | Low | Already true today. If it becomes a problem, `StartupPlan` is the single place to add skip-if-current logic. |

**The one thing explicitly *not* to do:** make `RunCommand` call `await new SetupCommand().Execute(...)`. That is the naive answer and exactly the trap described in observation #2 — two `HostBuilder`s, two DI universes. The entire point of this design is that there is only ever one container, and setup is a prefix of it.

---

## 8. Delivery plan

Each phase is independently shippable. The child-process hack stays in place until Phase 5, so every intermediate state is releasable.

### Phase 0 — Baseline guardrails (no behavior change)

Before touching anything, capture what the current system does so the refactor can be verified against a fixed reference.

* DI registration tests asserting the current count of each persistence lifecycle `IHostedService` in a setup container and in a run container, for RavenDB and EF Core.
* A characterization test capturing the observable setup sequence per app (ordered log lines / side effects).
* A container integration test run confirming `--setup-and-run` works end to end today.

**Exit criteria:** a green baseline that would fail if registration counts or setup ordering changed.

### Phase 1 — Unify persistence registration (primary app)

* Introduce `PersistenceMode`; make `AddInstaller` delegate to the single `AddPersistence(services, mode)`.
* Ensure `PersistenceFactory.Create` is called once per host.
* Verify the Phase 0 tests: exactly one lifecycle hosted service in the unified container.

**Exit criteria:** unified registration produces identical container shape to today's setup path; `--setup` and `--setup-and-run` both still work through the unchanged child-process route.
**Risk:** R1, R2. **Ship alone.**

### Phase 2 — Introduce `ISetupStep` + `SetupPipelineHostedService`

* Define `ISetupStep`, `StartupPlan`, and the orchestrator.
* Extract the inline `SetupCommand` work into `PersistenceInitializationStep`, `ProvisionQueuesStep`, `ApplyMigrationsStep`, `ProvisionBodyStorageStep`, `EventSourceStep`.
* `SetupCommand` becomes: build prefix host → `StartAsync` → `StopAsync`.

**Exit criteria:** the Phase 0 characterization test passes unchanged against the step-based implementation.

### Phase 3 — Split `AddServiceControl` into prefix and suffix

* Extract `AddServiceControlSetup` (the prefix) and have `AddServiceControl` call it first.
* `--setup` uses the prefix directly.

**Exit criteria:** `--setup`, `--maintenance`, `--import-failed-errors`, `--error-ingestion-only` all still behave identically; the prefix/suffix relationship is structural.

### Phase 4 — Single-host setup-and-run ⭐

* `HostArguments` gains `RunSetup`.
* `RunCommand` runs the plan via `BeforeStartAsync` when `RunSetup` is set.
* Collapse the two hosts in `ServiceControlComponentRunner` into one — this is the natural proof of the design.

**Exit criteria:** the full acceptance suite passes with a single host performing setup then run; the ordering test from R3 passes. **This is the milestone that proves the whole approach.**

### Phase 5 — Kill the hack

* Register `--setup-and-run` in `HostArguments` as a first-class option.
* Delete `IntegratedSetup.cs` and its call in all three `Program.cs` files.

**Exit criteria:** container integration tests pass with no child process; `docker logs` shows a single ordered stream.
**Risk:** R4, R5.

### Phase 6 — Make component setup declarative

* Rename `Setup` → `DeclareRequirements`; drop the `IHostApplicationBuilder` parameter; introduce `IComponentSetupDeclaration`.
* Mechanical refactor — the compiler proves no override used the parameter.

**Exit criteria:** compiles clean; all component tests pass.

### Phase 7 — Converge Audit

* Unify Audit's persistence abstraction with the primary app's (`PersistenceConfigurationFactory` → `IPersistence` + `PersistenceFactory`).
* Apply the prefix/suffix split and `StartupPlan`.
* Move `AuditIngestion` to `AfterStartedAsync`, deleting the registration-order comment.

**Exit criteria:** Audit's `--setup` and `--setup-and-run` both run in a single process; Audit acceptance tests pass.
**Risk:** R6. This is the largest remaining chunk — the persistence unification here is comparable in size to Phase 1.

### Phase 8 — Converge Monitoring

* Simplest: no persistence, setup is queue creation only.
* Apply the same shape.

**Exit criteria:** Monitoring `--setup-and-run` runs in a single process.

### Phase 9 — Extract the shared hosting stack

Once all three apps have the same shape, extract `AbstractCommand`, `CommandRunner`, `HostArguments` and the vendored `Options.cs` into `ServiceControl.Infrastructure.Hosting` — one copy instead of three.

**Exit criteria:** no duplicated `Options.cs`; all three apps use the shared dispatcher.

### Parallelism

Phases 0–5 are strictly sequential and confined to the primary app. Phase 6 can start alongside Phase 4. Phases 7 and 8 can run in parallel with each other once Phase 5 lands. Phase 9 must come last.

Suggested PR boundaries: **0**, **1**, **2**, **3**, **4**, **5**, **6**, **7a** (persistence unification), **7b** (Audit shape), **8**, **9** — roughly eleven PRs, each independently revertable.

---

## 9. Testing strategy

| Test | Purpose | Phase |
|---|---|---|
| DI registration count tests (RavenDB + EF Core, setup + run) | Catch double lifecycle registration (R1) | 0, 1 |
| Setup sequence characterization test | Detect ordering drift | 0, 2 |
| Setup-before-receive ordering test | Assert provisioning precedes first receive (R3) | 4 |
| Forced migration failure → exit code + stderr test | Windows installer contract (R4) | 5 |
| Collapsed acceptance harness (one host) | The core design proof | 4 |
| `StartupModeTests` rewritten against `StartupPlan` | Maintenance / import-failed-errors parity; removes the "blocks indefinitely" workaround | 2, 3 |
| Container integration suite (`servicecontrol.yml`) | End-to-end `--setup-and-run` for all three apps | 5, 7, 8 |
| Multi-transport matrix for queue provisioning | ASB topic creation, SQS/Rabbit/SQLServer queue creation still idempotent | 2 |
| Fresh-install-with-configured-schema-missing test | `RequireSchema`'s deliberate failure still surfaces identically | 5 |

---

## 10. Appendix — file inventory

**Entry points**
* `src/ServiceControl/Program.cs`, `src/ServiceControl.Audit/Program.cs`, `src/ServiceControl.Monitoring/Program.cs`
* `src/ServiceControl.Infrastructure/IntegratedSetup.cs` ← **to be deleted**

**Command machinery (triplicated)**
* `src/ServiceControl/Hosting/{HostArguments.cs, Options.cs, Commands/*}`
* `src/ServiceControl.Audit/Infrastructure/Hosting/{HostArguments.cs, Options.cs, Commands/*}`
* `src/ServiceControl.Monitoring/Hosting/{HostArguments.cs, Options.cs, Commands/*}`

**Composition roots**
* `src/ServiceControl/HostApplicationBuilderExtensions.cs` ← prefix/suffix split
* `src/ServiceControl.Audit/HostApplicationBuilderExtensions.cs`
* `src/ServiceControl.Monitoring/HostApplicationBuilderExtensions.cs`

**Persistence abstraction**
* `src/ServiceControl.Persistence/IPersistence.cs` ← `AddInstaller` removed
* `src/ServiceControl.Persistence/IDatabaseMigrator.cs`, `IBodyStorageInstaller.cs`
* `src/ServiceControl/Persistence/{PersistenceFactory.cs, PersistenceServiceCollectionExtensions.cs}`
* `src/ServiceControl.Persistence.RavenDB/{RavenPersistence.cs, RavenPersistenceLifecycleHostedService.cs, RavenEmbeddedPersistenceLifecycle.cs}`
* `src/ServiceControl.Persistence.EFCore.PostgreSql/{PostgreSqlPersistence.cs, PostgreSqlDatabaseMigrator.cs}`
* `src/ServiceControl.Audit/**` persistence factory (parallel abstraction — Phase 7)

**Component model**
* `src/ServiceControl/ServiceControlComponent.cs` ← `Setup` → `DeclareRequirements`
* `src/ServiceControl/ComponentInstallationContext.cs`
* `src/ServiceControl/{HostingComponent.cs, Recoverability/RecoverabilityComponent.cs, Licensing/LicensingComponent.cs, Monitoring/HeartbeatMonitoringComponent.cs, CustomChecks/CustomChecksComponent.cs}`

**Transport provisioning**
* `src/ServiceControl.Transports/TransportCustomization.cs`
* `src/ServiceControl.Transports.ASBS/ASBSTransportCustomization.cs`

**External contracts**
* `src/ServiceControlInstaller.Engine/Setup/InstanceSetup.cs`
* `src/ServiceControl/Dockerfile`, `src/container-integration-test/servicecontrol.yml`
* `src/ServiceControl*/Properties/launchSettings.json`, `src/ServiceControl*/Container-README.md`

**Test harness**
* `src/ServiceControl.AcceptanceTests/TestSupport/ServiceControlComponentRunner.cs`
* `src/ServiceControl.AcceptanceTests.RavenDB/StartupModeTests.cs`

**History**
* `e5d4bad60` — "'Setup and run' startup option to combine setup and run into one action (#4387)", 2024-08-23
* `3cb74ea07` — "Exit setup-and-run mode if setup fails (#4403)", 2024-08-30
* Issue `#4392` — "Redesign logic around Run and Setup commands", open, `Improvement` + `Refactoring`, 2024-08-22
