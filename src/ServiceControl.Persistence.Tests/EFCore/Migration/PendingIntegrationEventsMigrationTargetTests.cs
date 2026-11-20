namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation;

class PendingIntegrationEventsMigrationTargetTests : PersistenceTestBase
{
    readonly TargetWarnings targetLog = new();

    public PendingIntegrationEventsMigrationTargetTests() => RegisterServices = services => services.AddSingleton<ILoggerProvider>(targetLog);

    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    ExternalIntegrationRequestsDataStore Dispatcher => ServiceProvider.GetServices<IHostedService>().OfType<ExternalIntegrationRequestsDataStore>().Single();

    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.PendingIntegrationEvents)!;

    [Test]
    public async Task A_copied_request_is_dispatched_by_the_product_as_its_concrete_type_and_then_removed()
    {
        var dispatched = Array.Empty<object>();
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            dispatched = contexts;
            return Task.CompletedTask;
        });

        var result = await Write(("ExternalIntegrationDispatchRequests/1", new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "copied" } }));

        await Dispatcher.DispatchNow(TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(((TestDispatchContext)dispatched.Single()).Value, Is.EqualTo("copied"));
            Assert.That(await Rows(), Is.Zero, "the dispatcher deletes what it delivered, which is delivery rather than loss");
        }
    }

    [Test]
    public async Task A_request_with_no_context_is_skipped_as_a_required_value_missing()
    {
        var result = await Write(
            ("ExternalIntegrationDispatchRequests/1", new ExternalIntegrationDispatchRequest { DispatchContext = null! }),
            ("ExternalIntegrationDispatchRequests/2", new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "kept" } }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkipReasons[MigrationSkipReason.RequiredValueMissing], Is.EqualTo(1), "both columns are NOT NULL, and a throw would stop the category at this batch");
            Assert.That(await Target.Count(Category), Is.EqualTo(1), "Count is what --migration-verify reads, so it must see the row the copy wrote");
        }
    }

    [Test]
    public async Task A_request_whose_context_type_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        // The stored type name ends with the assembly name, so a long assembly name makes it longer than the column.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(new string('a', 460)), AssemblyBuilderAccess.Run);
        var longNamedContext = Activator.CreateInstance(assembly.DefineDynamicModule("Contexts").DefineType("Context", TypeAttributes.Public).CreateType())!;

        var result = await Write(
            ("ExternalIntegrationDispatchRequests/1", new ExternalIntegrationDispatchRequest { DispatchContext = longNamedContext }),
            ("ExternalIntegrationDispatchRequests/2", new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "kept" } }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "ExternalIntegrationDispatchRequests/1" }));
            Assert.That(result.SkipReasons[MigrationSkipReason.ValueTooLong], Is.EqualTo(1), "one value longer than its column fails the whole statement, so it is skipped instead");
            Assert.That(await Target.Count(Category), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task The_first_batch_that_copies_events_says_once_that_the_database_gives_them_new_ids()
    {
        var first = await Write(("ExternalIntegrationDispatchRequests/1", new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "first" } }));
        await Target.Write(
            Category,
            new MigrationBatch([new MigrationRow("ExternalIntegrationDispatchRequests/2", new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "second" } }, new Dictionary<string, object>())], "ExternalIntegrationDispatchRequests/2"),
            first.Saved);

        Assert.That(targetLog.Information, Has.Exactly(1).EqualTo($"Category {MigrationCategoryIds.PendingIntegrationEvents}: the database gives the copied pending integration events new ids. Nothing in SQL refers to the ids they had in the source."),
            "one line per category says the ids changed, rather than one per row or per batch");
    }

    Task<MigrationWriteResult> Write(params (string Id, ExternalIntegrationDispatchRequest Request)[] rows) =>
        Target.Write(
            Category,
            new MigrationBatch([.. rows.Select(row => new MigrationRow(row.Id, row.Request, new Dictionary<string, object>()))], rows[^1].Id),
            new MigrationCheckpoint(Category.Id, MigrationCategoryState.InProgress, rows[^1].Id, 0, 0, null, null, null, null, null, null));

    async Task<int> Rows()
    {
        using var scope = ServiceProvider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().ExternalIntegrationDispatchRequests.CountAsync();
    }

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
