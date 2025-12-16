# Testing Architecture

This document provides a comprehensive overview of the testing architecture in ServiceControl. It is intended to help developers understand how tests are structured and where to add tests for new functionality.

For a summary of test types, see [testing.md](testing.md). For manual testing scenarios, see [testing-scenarios.md](testing-scenarios.md).

## Test Projects Overview

The repository contains 28 test projects organized into several categories:

### Unit Test Projects

| Project | Purpose |
|---------|---------|
| `ServiceControl.UnitTests` | Primary instance unit tests |
| `ServiceControl.Audit.UnitTests` | Audit instance unit tests |
| `ServiceControl.Monitoring.UnitTests` | Monitoring instance unit tests |
| `ServiceControl.Infrastructure.Tests` | Shared infrastructure tests |
| `ServiceControl.Config.Tests` | WPF configuration UI tests |
| `ServiceControlInstaller.Engine.UnitTests` | Windows service installer tests |
| `ServiceControlInstaller.Packaging.UnitTests` | Packaging utilities tests |
| `Particular.LicensingComponent.UnitTests` | Licensing component tests |

### Persistence Test Projects

| Project | Purpose |
|---------|---------|
| `ServiceControl.Persistence.Tests` | Abstract persistence layer tests |
| `ServiceControl.Persistence.Tests.RavenDB` | RavenDB persistence implementation |
| `ServiceControl.Persistence.Tests.InMemory` | In-memory persistence tests |
| `ServiceControl.Audit.Persistence.Tests` | Audit persistence abstractions |
| `ServiceControl.Audit.Persistence.Tests.RavenDB` | Audit RavenDB tests |

### Acceptance Test Projects

| Project | Purpose |
|---------|---------|
| `ServiceControl.AcceptanceTests` | Primary instance shared acceptance test code |
| `ServiceControl.AcceptanceTests.RavenDB` | Primary instance with RavenDB |
| `ServiceControl.Audit.AcceptanceTests` | Audit instance shared acceptance test code |
| `ServiceControl.Audit.AcceptanceTests.RavenDB` | Audit with RavenDB |
| `ServiceControl.Monitoring.AcceptanceTests` | Monitoring instance acceptance tests |
| `ServiceControl.MultiInstance.AcceptanceTests` | Multi-instance integration tests |

### Transport Test Projects

| Project | Filter Value |
|---------|--------------|
| `ServiceControl.Transports.Tests` | Default (Learning Transport) |
| `ServiceControl.Transports.ASBS.Tests` | AzureServiceBus |
| `ServiceControl.Transports.ASQ.Tests` | AzureStorageQueues |
| `ServiceControl.Transports.Msmq.Tests` | MSMQ |
| `ServiceControl.Transports.PostgreSql.Tests` | PostgreSql |
| `ServiceControl.Transports.RabbitMQClassicConventionalRouting.Tests` | RabbitMQ |
| `ServiceControl.Transports.RabbitMQClassicDirectRouting.Tests` | RabbitMQ |
| `ServiceControl.Transports.RabbitMQQuorumConventionalRouting.Tests` | RabbitMQ |
| `ServiceControl.Transports.RabbitMQQuorumDirectRouting.Tests` | RabbitMQ |
| `ServiceControl.Transports.SqlServer.Tests` | SqlServer |
| `ServiceControl.Transports.SQS.Tests` | SQS |

## Testing Framework and Conventions

All projects use:

- **Framework**: NUnit 3.x
- **Test Adapter**: NUnit3TestAdapter
- **SDK**: Microsoft.NET.Test.Sdk
- **Target Framework**: `net8.0` (Windows-specific tests use `net8.0-windows`)

### Test Class Structure

```csharp
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]  // Each test gets fresh instance
public class MyTests
{
    [SetUp]
    public async Task Setup()
    {
        // Per-test initialization
    }

    [TearDown]
    public async Task TearDown()
    {
        // Per-test cleanup
    }

    [Test]
    public async Task Should_do_something()
    {
        // Arrange, Act, Assert
    }
}
```

### Approval Testing

Used for API contracts and serialization verification:

```csharp
[Test]
public void VerifyApiContract()
{
    var result = GetApiContract();
    Approver.Verify(result);
}
```

Baseline files stored in `ApprovalFiles/` directories with naming pattern: `{TestName}.{Method}.approved.txt`

## Transport Filtering System

Tests can be filtered by transport using the `ServiceControl_TESTS_FILTER` environment variable.

### Filter Attributes

Located in `src/TestHelper/IncludeInTestsAttribute.cs`:

| Attribute | Filter Value |
|-----------|--------------|
| `[IncludeInDefaultTests]` | Default |
| `[IncludeInAzureServiceBusTests]` | AzureServiceBus |
| `[IncludeInAzureStorageQueuesTests]` | AzureStorageQueues |
| `[IncludeInMsmqTests]` | MSMQ |
| `[IncludeInPostgreSqlTests]` | PostgreSql |
| `[IncludeInRabbitMQTests]` | RabbitMQ |
| `[IncludeInSqlServerTests]` | SqlServer |
| `[IncludeInAmazonSqsTests]` | SQS |

### Usage

Apply at assembly level to include entire test project:

```csharp
[assembly: IncludeInDefaultTests()]
```

Run filtered tests:

```powershell
$env:ServiceControl_TESTS_FILTER = "Default"
dotnet test src/ServiceControl.sln
```

## Base Classes and Utilities

### Unit Test Base Classes

For simple unit tests, no special base class is required. Use standard NUnit patterns.

### Persistence Test Base Class

Location: `src/ServiceControl.Persistence.Tests/PersistenceTestBase.cs`

```csharp
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class PersistenceTestBase
{
    protected PersistenceSettings PersistenceSettings { get; }
    protected IErrorMessageDataStore ErrorStore { get; }
    protected IRetryDocumentDataStore RetryStore { get; }
    protected IBodyStorage BodyStorage { get; }
    protected IRetryBatchesDataStore RetryBatchesStore { get; }
    protected IMessageRedirectsDataStore MessageRedirectsDataStore { get; }
    protected IMonitoringDataStore MonitoringDataStore { get; }
    protected ICustomChecksDataStore CustomChecks { get; }
    protected IArchiveMessages ArchiveMessages { get; }
    protected IEventLogDataStore EventLogDataStore { get; }
    protected IServiceProvider ServiceProvider { get; }

    // Async setup/teardown with embedded database management
    [SetUp]
    public async Task Setup();

    [TearDown]
    public async Task TearDown();
}
```

### RavenDB Persistence Test Base

Location: `src/ServiceControl.Persistence.Tests.RavenDB/RavenPersistenceTestBase.cs`

Extends `PersistenceTestBase` with direct RavenDB access:

```csharp
public abstract class RavenPersistenceTestBase : PersistenceTestBase
{
    protected IDocumentStore DocumentStore { get; }
    protected IRavenSessionProvider SessionProvider { get; }

    // Debug helper - blocks test to inspect embedded database
    protected void BlockToInspectDatabase();
}
```

### Acceptance Test Base Class

Location: `src/ServiceControl.AcceptanceTests/TestSupport/AcceptanceTest.cs`

```csharp
public abstract class AcceptanceTest : NServiceBusAcceptanceTest
{
    protected HttpClient HttpClient { get; }
    protected JsonSerializerOptions SerializerOptions { get; }
    protected IDomainEvents DomainEvents { get; }
    protected Action<Settings> SetSettings { get; set; }
    protected Action<EndpointConfiguration> CustomConfiguration { get; set; }
    protected Action<IHostApplicationBuilder> CustomizeHostBuilder { get; set; }

    // Create a test scenario
    protected IScenarioWithEndpointBehavior<T> Define<T>()
        where T : ScenarioContext, new();
}
```

### Transport Test Base Class

Location: `src/ServiceControl.Transports.Tests/TransportTestFixture.cs`

```csharp
public abstract class TransportTestFixture
{
    protected TransportTestsConfiguration Configuration { get; }

    // Setup test transport infrastructure
    protected Task ProvisionQueues(params string[] queueNames);

    // Start listening for messages
    protected Task<IMessageReceiver> StartQueueIngestor(string queueName);

    // Monitor queue depth
    protected Task StartQueueLengthProvider(Action<QueueLengthEntry[], long> callback);

    // Send and receive test messages
    protected Task SendAndReceiveMessages(int messageCount);
}
```

## Test Infrastructure Components

### Shared Embedded RavenDB Server

Location: `src/ServiceControl.Persistence.Tests.RavenDB/SharedEmbeddedServer.cs`

Provides singleton embedded RavenDB server with:

- Semaphore-based concurrency control
- Automatic database cleanup
- Dynamic port assignment
- Test database isolation with GUID-based names

### Port Utility

Location: `src/TestHelper/PortUtility.cs`

Finds available ports for test services:

```csharp
var port = PortUtility.FindAvailablePort(startingPort: 33333);
```

### App Settings Fixture

Location: Various test projects, `AppSettingsFixture.cs`

One-time assembly setup that loads `app.config` settings into `ConfigurationManager`.

## Directory Structure Within Test Projects

Unit test projects are typically organized by domain:

```text
ServiceControl.UnitTests/
├── API/                    # API controller tests
├── Recoverability/         # Retry and recovery logic
├── Infrastructure/         # Extension and utility tests
├── Monitoring/             # Monitoring component tests
├── Notifications/          # Notification infrastructure
├── Licensing/              # License validation tests
├── BodyStorage/            # Message body storage
├── ExternalIntegrations/   # External system integration
├── ApprovalFiles/          # Approval test baselines
└── ...
```

## Adding Tests for New Functionality

### Decision Tree: Where Should My Test Go?

```text
Is it testing a single class/method in isolation?
├─ Yes → Unit Tests (ServiceControl.UnitTests, etc.)
│
├─ No → Does it require persistence?
│   ├─ Yes → Persistence Tests (ServiceControl.Persistence.Tests.*)
│   │
│   └─ No → Does it require transport infrastructure?
│       ├─ Yes → Transport Tests (ServiceControl.Transports.*.Tests)
│       │
│       └─ No → Does it require full ServiceControl instance?
│           ├─ Yes → Does it involve multiple instances?
│           │   ├─ Yes → MultiInstance.AcceptanceTests
│           │   └─ No → AcceptanceTests (Primary/Audit/Monitoring)
│           │
│           └─ No → Unit Tests with mocks
```

### Example: Adding Tests for Forward Headers Configuration

For a feature like forward headers, tests focus on the **settings/parsing logic**. The middleware itself is a thin wrapper around ASP.NET Core's `UseForwardedHeaders()` and doesn't require separate unit testing.

#### Unit Tests for Configuration Parsing

Location: `src/ServiceControl.UnitTests/Infrastructure/Settings/ForwardedHeadersSettingsTests.cs`

```csharp
/// <summary>
/// Tests for ForwardedHeadersSettings which is shared infrastructure code
/// used by all three instance types. Testing with one namespace is sufficient.
/// </summary>
[TestFixture]
public class ForwardedHeadersSettingsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", null);
    }

    [Test]
    public void Should_parse_known_proxies_from_comma_separated_list()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_FORWARDEDHEADERS_KNOWNPROXIES", "127.0.0.1,10.0.0.5");

        var settings = new ForwardedHeadersSettings(TestNamespace);

        Assert.That(settings.KnownProxiesRaw, Has.Count.EqualTo(2));
    }
}
```

#### End-to-End Testing

For middleware configuration like forward headers, end-to-end behavior is best verified through:

1. **Manual testing** with curl - documented in [local-forward-headers-testing.md](local-forward-headers-testing.md)
2. **Acceptance tests** (optional) - only if automated verification is needed

The middleware extension (`UseServiceControlForwardedHeaders`) is configuration wiring that delegates to ASP.NET Core's built-in middleware. Unit testing it would require mocking `WebApplication` and would essentially test ASP.NET Core rather than our code.

### Example: Adding Tests for API Endpoints

#### Unit Test for Controller Logic

```csharp
[TestFixture]
public class MyControllerTests
{
    [Test]
    public async Task Get_should_return_expected_data()
    {
        var mockDataStore = new Mock<IMyDataStore>();
        mockDataStore.Setup(x => x.GetData()).ReturnsAsync(expectedData);

        var controller = new MyController(mockDataStore.Object);
        var result = await controller.Get();

        Assert.That(result, Is.EqualTo(expectedData));
    }
}
```

#### Acceptance Test for Full API Flow

```csharp
[TestFixture]
public class When_calling_my_api_endpoint : AcceptanceTest
{
    [Test]
    public async Task Should_return_correct_response()
    {
        await Define<MyContext>()
            .WithEndpoint<ServiceControlEndpoint>()
            .Done(async c =>
            {
                var response = await HttpClient.GetAsync("/api/my-endpoint");
                if (response.IsSuccessStatusCode)
                {
                    c.Response = await response.Content.ReadAsStringAsync();
                    return true;
                }
                return false;
            })
            .Run();

        Assert.That(context.Response, Is.Not.Null);
    }

    class MyContext : ScenarioContext
    {
        public string Response { get; set; }
    }
}
```

## Test Patterns and Best Practices

### Async-First Testing

All test setup, execution, and teardown support async patterns:

```csharp
[Test]
public async Task Should_handle_async_operation()
{
    var result = await SomeAsyncOperation();
    Assert.That(result, Is.True);
}
```

### Instance Per Test Case

Use `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` for test isolation:

```csharp
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MyTests
{
    // Each test gets a fresh instance of this class
}
```

### Dependency Injection in Tests

Access services through `IServiceProvider`:

```csharp
protected IServiceProvider ServiceProvider { get; }

[Test]
public void Should_resolve_service()
{
    var myService = ServiceProvider.GetRequiredService<IMyService>();
    // Use service
}
```

### Using WaitUntil for Async Verification

```csharp
await WaitUntil(async () =>
{
    var result = await CheckCondition();
    return result.IsReady;
}, timeoutInSeconds: 30);
```

### Test Timeout Handling

Transport tests use configured timeouts:

```csharp
protected TimeSpan TestTimeout => TimeSpan.FromSeconds(60);
```

## Running Tests

### All Tests

```bash
dotnet test src/ServiceControl.sln
```

### By Transport Filter

```powershell
$env:ServiceControl_TESTS_FILTER = "Default"
dotnet test src/ServiceControl.sln
```

### Specific Project

```bash
dotnet test src/ServiceControl.UnitTests/ServiceControl.UnitTests.csproj
```

### Single Test by Name

```bash
dotnet test src/ServiceControl.UnitTests/ServiceControl.UnitTests.csproj --filter "FullyQualifiedName~MyTestMethodName"
```

### With Verbose Output

```bash
dotnet test src/ServiceControl.sln --logger "console;verbosity=detailed"
```

## Environment Variables for Transport Tests

| Transport | Environment Variable |
|-----------|---------------------|
| SQL Server | `ServiceControl_TransportTests_SQL_ConnectionString` |
| Azure Service Bus | `ServiceControl_TransportTests_ASBS_ConnectionString` |
| Azure Storage Queues | `ServiceControl_TransportTests_ASQ_ConnectionString` |
| RabbitMQ | `ServiceControl_TransportTests_RabbitMQ_ConnectionString` |
| AWS SQS | `ServiceControl_TransportTests_SQS_*` |
| PostgreSQL | `ServiceControl_TransportTests_PostgreSql_ConnectionString` |

## Summary

When adding tests for new functionality:

1. **Start with unit tests** for isolated logic (configuration parsing, algorithms, helpers)
2. **Add persistence tests** if the feature involves data storage
3. **Add acceptance tests** for end-to-end API and behavior verification
4. **Add transport tests** if the feature involves transport-specific behavior
5. Follow existing patterns in similar test files
6. Use appropriate base classes to reduce boilerplate
7. Ensure tests run with the Default filter for CI compatibility
