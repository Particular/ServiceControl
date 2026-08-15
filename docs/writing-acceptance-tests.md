# Writing acceptance tests

## What these tests are for

An acceptance test starts a full ServiceControl instance and drives it over the HTTP API, exactly as ServicePulse does. See [Testing](testing.md) for the suites and how to run them.

This is what makes them different from the other suites. Component logic belongs in unit tests, and storage belongs in persistence tests. Both of those are faster and easier to debug. An acceptance test is worth its cost when it proves something a user can see: that a journey through ServicePulse still works, against a real instance and a real persister.

So the question to start from is not "which endpoint am I testing" but "what is someone trying to do".

### The transport is always LearningTransport

Every acceptance test runs on LearningTransport, a file system transport with no broker behind it. A feature that asks the transport for something therefore takes a different branch here than it does in production. Throughput collection is the clearest case: RabbitMQ, Azure Service Bus, SQS, SQL Server and PostgreSQL each register an `IBrokerThroughputQuery`, while Learning and MSMQ do not, so code that reads throughput sees no broker unless the test provides one.

So before writing a scenario, check whether the feature asks the transport for anything. If it does, choose which branch the test covers, register a fake if you want the other one, and put the branch in the test name rather than in a comment. A name such as `When_creating_a_usage_report_on_a_non_broker_transport` is visible in the test run and in the failure, where a comment is not, and it leaves room for the other branch beside it.

## Start from the journey

Write the test as the journey, and let it call every route the journey needs. "Create a usage report to send to Particular" is one test covering eight `api/licensing` routes, instead of eight tests covering one route each.

There are two reasons for this. A test per route can show that each route answers. Only a journey can show that the id one call returns is accepted by the next call, and that is where the failures users notice come from. A journey is also cheaper to run, because starting ServiceControl takes most of the time. One scenario across eight routes costs about an eighth of eight tests across one route each.

Drive it with the `Sequence` helper, with one `Do("step", …)` per thing the user does:

```csharp
await Define<Context>()
    .WithEndpoint<MonitoringInstance>()
    .Do("Wait for the throughput data to be recorded", async _ => …)
    .Do("Correct the queue that is not an NServiceBus endpoint", async _ => …)
    .Do("Redact the customer name in the queue names", async _ => …)
    .Do("Download the report", async _ => …)
    .Done(_ => true)
    .Run();
```

Each step logs `Advancing from X to Y`. If the scenario stops making progress, the log names the step it stopped on, instead of the test failing with a timeout that tells you nothing. `When_creating_a_usage_report_on_a_non_broker_transport` is the worked example, and its name is doing the job described above: it says which branch it covers, so the broker one can sit beside it without either being mistaken for the other.

Not everything is a journey. Edge cases, such as posting a retry for an id that does not exist, are separate focused tests next to the scenario, not extra steps inside it.

## Arrange the data the way it really arrives

Feed the system the way production does, rather than writing to the data store directly. The usage report scenario sends the same throughput message that a monitoring instance sends. The test therefore goes through the queue, the hosted service and the persistence before it asserts anything. Writing to the store directly would have skipped all three, and the test would still have passed.

## Assert what the caller depends on

A 200 and a non-empty body only show that the route is there, which is rarely the part that breaks. Assert what the caller needs: the shape ServicePulse reads, the status code it branches on, the effect the action has on the next request.

The report scenario asserts that a name the user redacted does not appear in the downloaded file, and that a queue they marked as "not an endpoint" is still marked that way in the report. A user would be harmed if either broke, and a status code shows neither.

## Expect the domain to have rules

When a scenario hangs, it is often the system telling you a rule you did not know about. The report scenario first recorded throughput for today, and then waited until the 90 second timeout. The reason is that a usage report counts only complete days, and ignores a partial one on purpose.

So when a step will not go green, read the code it is waiting on before you add longer timeouts or retries. Once you find the rule, write it in the test as a comment, because the next person will assume what you assumed.

## Caveats

One idea connects all of these: **a test must fail if the thing it sets up does not take effect.** Below are the ways that has gone wrong in this suite. Two of them went unnoticed for years.

### The double that is registered but never used

Register a double as the service type its collaborator asks for, never as its own concrete type.

```csharp
// Wrong: nothing resolves CounterEnricher, so the double never runs and the test still passes.
builder.Services.AddSingleton<CounterEnricher>();

// Right: the import pipeline injects IEnumerable<IEnrichImportedErrorMessages>.
builder.Services.AddSingleton<IEnrichImportedErrorMessages, CounterEnricher>();
```

First check how the collaborator asks for its dependency, because the two shapes behave differently when a test adds to them:

- **One instance**, such as `ReturnToSenderDequeuer` asking for a `ReturnToSender`. `CustomizeHostBuilder` runs after all production registration, so registering the same service type again replaces it with the double.
- **A collection**, such as `IEnumerable<IEnrichImportedErrorMessages>` or `GetServices<ICustomCheck>()`. A later registration is *added to* the collection. The production implementation is still there, and still runs.

### The registration that only says it replaces another

In the collection case, find the production registration and remove it, instead of adding a second one. A comment saying that a registration replaces another one does not make it true:

```csharp
// Consumed through GetServices<ICustomCheck>(), so the shorter test interval only takes
// effect if the production registration is replaced rather than added alongside it.
var productionRegistration = builder.Services.Single(registration =>
    registration.ServiceType == typeof(ICustomCheck) &&
    registration.ImplementationType == typeof(CriticalErrorCustomCheck));

builder.Services.Remove(productionRegistration);
builder.Services.AddSingleton<ICustomCheck>(_ => new CriticalErrorCustomCheck(TimeSpan.FromSeconds(1)));
```

The `Single` call matters. If the production registration moves or changes, the test fails straight away and says so. Without it, the test would quietly go back to testing the production configuration.

### The assertion that could not have failed

```csharp
await Define<Context>().Done(ctx => ctx.Done).Run();

Assert.That(context.Done, Is.True); // The runner already waited for this. A false flag times out instead.
```

Assert the thing the test is named for. It is fine for a message handler to set a flag while the scenario waits for something else. Ask whether the assertion could fail while the scenario still finished.

### The double whose effect is never checked

Registering the double correctly is only half the job. If the assertion would also pass with the double absent, the test does not cover the double at all. Assert something only the double can produce: the counter it incremented, the failure it injected, the message it swallowed.

### The assertion that only one persister can satisfy

The suite runs against every persister. An assertion about how RavenDB happens to store or trim something says nothing about ServiceControl's behaviour. It also ends up on another persister's exclusion list, where it looks like a missing feature instead of a test that asks for too much. Assert what every persister has to do to be correct.

### The setup that nothing reads

Headers put into a dictionary nothing reads, constants nothing compares against, fixtures left behind after an assertion was deleted. Each one is harmless by itself. Together they make a test look like it covers more than it does, which is how everything above gets through review.

## Before you open the PR

**Make it fail on purpose.** Break the thing the test protects, run it, and read the message. If it still passes, or if it fails with something another person cannot act on, the test is not finished yet. This one run checks every caveat listed above.

**Run it on a second persister.** A test that passes on Raven and is never run on SqlServer or PostgreSQL will be excluded later by someone who knows less about it than you do now.

**Cover a new route in the PR that adds it.** Nothing in the suite notices an uncovered route.
