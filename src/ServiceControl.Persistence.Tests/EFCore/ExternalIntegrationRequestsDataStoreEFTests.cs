namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation;

// DispatchNow bypasses the timer/signal wait, so these tests are deterministic instead of relying on WaitUntil(...) polling.
// One caveat: the background dispatch loop is live during these tests so it can race an explicit DispatchNow call for the same batch.
// `drainLock` stops the two from double-processing it, but not which one gets there first.
// Tests that care which specific call throws need to tolerate either side of that race.
class ExternalIntegrationRequestsDataStoreEFTests : PersistenceTestBase
{
    EFPersisterSettings EFSettings => (EFPersisterSettings)PersistenceSettings;

    ExternalIntegrationRequestsDataStore Store =>
        ServiceProvider.GetServices<IHostedService>().OfType<ExternalIntegrationRequestsDataStore>().Single();

    [Test]
    public async Task DispatchNow_dispatches_available_rows_and_removes_them()
    {
        var dispatched = Array.Empty<object>();
        ExternalIntegrationStore.Subscribe((contexts, cancellationToken) =>
        {
            dispatched = contexts;
            return Task.CompletedTask;
        });

        await ExternalIntegrationStore.StoreDispatchRequest([Request("one")]);

        await Store.DispatchNow(TestContext.CurrentContext.CancellationToken);

        Assert.That(((TestDispatchContext)dispatched.Single()).Value, Is.EqualTo("one"));
        Assert.That(await CountRows(), Is.Zero);
    }

    [Test]
    public async Task DispatchNow_leaves_a_failed_batch_in_place_and_removes_it_once_a_retry_succeeds()
    {
        var attempts = 0;
        ExternalIntegrationStore.Subscribe((_, cancellationToken) =>
        {
            attempts++;

            if (attempts == 1)
            {
                throw new InvalidOperationException("Simulated callback failure");
            }

            return Task.CompletedTask;
        });

        await ExternalIntegrationStore.StoreDispatchRequest([Request("retry-me")]);

        // The background dispatch loop is live for the whole test (StoreDispatchRequest above
        // wakes it via the in-process signal), so it races these DispatchNow calls for the row.
        // `drainLock` stops them double-processing the same batch, but not which of the two performs
        // the failing first attempt vs. the succeeding retry - so tolerate the one expected failure
        // landing on whichever call hits it, and keep going until the row is gone.
        for (var i = 0; i < 5 && await CountRows() > 0; i++)
        {
            try
            {
                await Store.DispatchNow(TestContext.CurrentContext.CancellationToken);
            }
            catch (InvalidOperationException)
            {
            }
        }

        Assert.That(attempts, Is.EqualTo(2), "exactly one failed attempt followed by one successful retry");
        Assert.That(await CountRows(), Is.Zero, "the row should be removed once the retry succeeds");
    }

    [Test]
    public async Task DispatchNow_drains_more_than_one_batch_when_the_backlog_exceeds_the_configured_batch_size()
    {
        EFSettings.ExternalIntegrationsDispatchingBatchSize = 2;

        var callbackInvocations = 0;
        var dispatchedCount = 0;
        ExternalIntegrationStore.Subscribe((contexts, cancellationToken) =>
        {
            callbackInvocations++;
            dispatchedCount += contexts.Length;
            return Task.CompletedTask;
        });

        await ExternalIntegrationStore.StoreDispatchRequest(Enumerable.Range(0, 5).Select(i => Request($"item-{i}")).ToList());

        await Store.DispatchNow(TestContext.CurrentContext.CancellationToken);

        Assert.That(dispatchedCount, Is.EqualTo(5));
        Assert.That(callbackInvocations, Is.EqualTo(3), "5 rows at a batch size of 2 should dispatch as 2 + 2 + 1");
        Assert.That(await CountRows(), Is.Zero);
    }

    [Test]
    public async Task DispatchNow_is_a_noop_when_nothing_is_stored()
    {
        ExternalIntegrationStore.Subscribe((_, cancellationToken) => throw new InvalidOperationException("Should not be called"));

        Assert.DoesNotThrowAsync(() => Store.DispatchNow(TestContext.CurrentContext.CancellationToken));
    }

    static ExternalIntegrationDispatchRequest Request(string value) => new() { DispatchContext = new TestDispatchContext { Value = value } };

    async Task<int> CountRows()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.ExternalIntegrationDispatchRequests.CountAsync(TestContext.CurrentContext.CancellationToken);
    }

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
