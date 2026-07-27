namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;

class FailedErrorImportTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Stores_and_replays_a_failed_import()
    {
        var headers = WellFormedHeaders();
        var body = Encoding.UTF8.GetBytes("<order>1</order>");

        await StoreImport(headers, body, nativeId: "native-1");

        Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.True);

        var replayed = await Replay();

        Assert.That(replayed, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed[0].Id, Is.EqualTo("native-1"));
            Assert.That(replayed[0].Headers, Is.EqualTo(headers));
            Assert.That(replayed[0].Body, Is.EqualTo(body));
        }

        Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.False);
    }

    [Test]
    public async Task Round_trips_a_binary_body_with_nul_bytes()
    {
        var headers = WellFormedHeaders();
        var body = new byte[] { 0x00, 0x01, 0x02, 0x00, 0xFF, 0x00 };

        await StoreImport(headers, body, nativeId: "native-1");

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(replayed[0].Body, Is.EqualTo(body));
            Assert.That(RecordedBodies.Written, Is.Empty);
        }
    }

    [Test]
    public async Task Spills_a_large_body_to_external_storage_and_replays_it()
    {
        var headers = WellFormedHeaders();
        var body = LargeBody();
        var externalId = FailedErrorImportEntity.ExternalBodyId(FailedErrorImport.DeriveKey(headers, "native-1"));

        await StoreImport(headers, body, nativeId: "native-1");

        Assert.That(RecordedBodies.Written.Select(written => written.BodyId), Does.Contain(externalId));

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(replayed[0].Body, Is.EqualTo(body));
            Assert.That(RecordedBodies.Deleted, Does.Contain(externalId));
        }

        Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.False);
    }

    [Test]
    public async Task Repeated_failure_of_the_same_message_keeps_one_row_with_the_latest_details()
    {
        var headers = WellFormedHeaders();

        await StoreImport(headers, Encoding.UTF8.GetBytes("first"), "first failure", "native-1");
        await StoreImport(headers, Encoding.UTF8.GetBytes("second"), "second failure", "native-1");

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(replayed[0].Body, Is.EqualTo(Encoding.UTF8.GetBytes("second")));
        }
    }

    [Test]
    public async Task Stores_and_replays_a_message_with_no_derivable_endpoint()
    {
        var headers = new Dictionary<string, string>();

        await StoreImport(headers, Encoding.UTF8.GetBytes("body"), nativeId: "native-1");
        await StoreImport(headers, Encoding.UTF8.GetBytes("body-again"), nativeId: "native-1");

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(replayed[0].Id, Is.EqualTo("native-1"));
            Assert.That(replayed[0].Body, Is.EqualTo(Encoding.UTF8.GetBytes("body-again")));
        }
    }

    [Test]
    public async Task A_failing_re_import_is_left_behind_while_the_rest_are_processed()
    {
        await Store(
            Import("native-1", BaseTime),
            Import("native-2", BaseTime.AddSeconds(1)),
            Import("native-3", BaseTime.AddSeconds(2)));

        var replayed = new List<string>();
        await FailedImportStore.ProcessFailedErrorImports(
            message =>
            {
                replayed.Add(message.Id);
                return message.Id == "native-2" ? throw new InvalidOperationException("boom") : Task.CompletedTask;
            },
            TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Is.EqualTo(new[] { "native-1", "native-2", "native-3" }));
            Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.True);
        }

        var secondRun = new List<string>();
        await FailedImportStore.ProcessFailedErrorImports(
            message => { secondRun.Add(message.Id); return Task.CompletedTask; },
            TestContext.CurrentContext.CancellationToken);

        Assert.That(secondRun, Is.EqualTo(new[] { "native-2" }));
    }

    [Test]
    public async Task Replays_across_multiple_pages()
    {
        var imports = Enumerable.Range(0, 250)
            .Select(i => Import($"native-{i:D4}", BaseTime.AddSeconds(i)))
            .ToArray();

        await Store(imports);

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(250));
            Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.False);
        }
    }

    [Test]
    public async Task Stops_replaying_when_cancelled()
    {
        await Store(
            Import("native-1", BaseTime),
            Import("native-2", BaseTime.AddSeconds(1)),
            Import("native-3", BaseTime.AddSeconds(2)));

        using var cts = new CancellationTokenSource();
        var replayed = new List<string>();

        await FailedImportStore.ProcessFailedErrorImports(
            message =>
            {
                replayed.Add(message.Id);
                cts.Cancel();
                return Task.CompletedTask;
            },
            cts.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.True);
        }
    }

    [Test]
    public async Task Retention_sweep_does_not_touch_failed_imports()
    {
        await Store(Import("native-1", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        await RunRetentionSweep();

        Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.True);
    }

    [Test]
    public async Task A_failing_external_body_delete_does_not_fail_the_re_import()
    {
        var headers = WellFormedHeaders();
        var body = LargeBody();
        var externalId = FailedErrorImportEntity.ExternalBodyId(FailedErrorImport.DeriveKey(headers, "native-1"));

        await StoreImport(headers, body, nativeId: "native-1");
        RecordedBodies.FailDeleteFor.Add(externalId);

        var replayed = await Replay();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1));
            Assert.That(replayed[0].Body, Is.EqualTo(body));
            Assert.That(await FailedImportStore.QueryContainsFailedImports(), Is.False);
        }
    }

    IFailedErrorImportDataStore FailedImportStore => ServiceProvider.GetRequiredService<IFailedErrorImportDataStore>();

    Task StoreImport(Dictionary<string, string> headers, byte[] body, string exceptionInfo = "boom", string nativeId = null)
    {
        nativeId ??= Guid.NewGuid().ToString();

        return ErrorStore.StoreFailedErrorImport(new FailedErrorImport
        {
            Id = FailedErrorImport.MakeDocumentId(FailedErrorImport.DeriveKey(headers, nativeId)),
            Message = new FailedTransportMessage { Id = nativeId, Headers = headers, Body = body },
            ExceptionInfo = exceptionInfo
        });
    }

    async Task<List<FailedTransportMessage>> Replay()
    {
        var replayed = new List<FailedTransportMessage>();

        await FailedImportStore.ProcessFailedErrorImports(
            message => { replayed.Add(message); return Task.CompletedTask; },
            TestContext.CurrentContext.CancellationToken);

        return replayed;
    }

    byte[] LargeBody()
    {
        var body = new byte[EFSettings.BodyStorage.MaxBodySizeToStore + 1];
        Random.Shared.NextBytes(body);
        return body;
    }

    static FailedErrorImportEntity Import(string nativeId, DateTime failedAt) => new()
    {
        UniqueMessageId = DeterministicGuid.MakeId(nativeId),
        FailedAt = failedAt,
        MessageId = nativeId,
        HeadersJson = "{}",
        Body = Encoding.UTF8.GetBytes(nativeId),
        BodyStoredExternally = false,
        ExceptionInfo = "boom"
    };

    static Dictionary<string, string> WellFormedHeaders() => new()
    {
        [Headers.MessageId] = "m1",
        [Headers.ProcessingEndpoint] = "Sales",
        [Headers.ContentType] = "text/xml"
    };

    static readonly DateTime BaseTime = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
}
