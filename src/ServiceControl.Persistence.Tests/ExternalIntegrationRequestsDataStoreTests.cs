namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;

class ExternalIntegrationRequestsDataStoreTests : PersistenceTestBase
{
    const int DefaultBatchSize = 100;

    [Test]
    public async Task Stored_request_is_dispatched_to_the_subscriber_and_removed()
    {
        var dispatched = new ConcurrentBag<object>();
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            foreach (var context in contexts)
            {
                dispatched.Add(context);
            }

            return Task.CompletedTask;
        });

        await ExternalIntegrationStore.StoreDispatchRequest([Request("one")]);

        await WaitUntil(() => Task.FromResult(dispatched.Count == 1), "the stored request to be dispatched");

        Assert.That(((TestDispatchContext)dispatched.Single()).Value, Is.EqualTo("one"));
    }

    [Test]
    public async Task More_requests_than_the_batch_size_are_dispatched_across_multiple_callback_invocations()
    {
        var callbackInvocations = 0;
        var dispatched = new ConcurrentBag<object>();
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            Interlocked.Increment(ref callbackInvocations);

            foreach (var context in contexts)
            {
                dispatched.Add(context);
            }

            return Task.CompletedTask;
        });

        var requests = Enumerable.Range(0, DefaultBatchSize + 1).Select(i => Request($"item-{i}")).ToList();
        await ExternalIntegrationStore.StoreDispatchRequest(requests);

        await WaitUntil(() => Task.FromResult(dispatched.Count == requests.Count), "every stored request to be dispatched");

        Assert.That(callbackInvocations, Is.GreaterThanOrEqualTo(2),
            "a backlog larger than the configured batch size should be dispatched across more than one callback invocation");
    }

    [Test]
    public async Task Concurrent_stores_are_all_eventually_dispatched()
    {
        var dispatched = new ConcurrentBag<object>();
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            foreach (var context in contexts)
            {
                dispatched.Add(context);
            }

            return Task.CompletedTask;
        });

        const int concurrentStores = 20;
        await Task.WhenAll(Enumerable.Range(0, concurrentStores)
            .Select(i => ExternalIntegrationStore.StoreDispatchRequest([Request($"concurrent-{i}")])));

        await WaitUntil(() => Task.FromResult(dispatched.Count == concurrentStores), "every concurrently stored request to be dispatched");
    }

    [Test]
    public async Task Callback_failure_does_not_stop_future_dispatch()
    {
        var attempts = 0;
        var dispatched = new ConcurrentBag<object>();
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("Simulated callback failure");
            }

            foreach (var context in contexts)
            {
                dispatched.Add(context);
            }

            return Task.CompletedTask;
        });

        await ExternalIntegrationStore.StoreDispatchRequest([Request("retry-me")]);

        // The failed first attempt is not removed, so the same request is redelivered once the
        // dispatcher recovers. Recovery is paced by RepeatedFailuresOverTimeCircuitBreaker's own
        // ~20s "armed" backoff on both backends, hence the longer-than-usual timeout.
        await WaitUntil(() => Task.FromResult(dispatched.Count == 1),
            "the request to be redelivered and dispatched after the failed attempt", TimeSpan.FromSeconds(40));

        Assert.That(attempts, Is.GreaterThanOrEqualTo(2));
    }

    static ExternalIntegrationDispatchRequest Request(string value) => new() { DispatchContext = new TestDispatchContext { Value = value } };

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
