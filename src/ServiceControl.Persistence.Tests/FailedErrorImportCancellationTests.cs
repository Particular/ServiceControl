namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Operations;

[TestFixture]
class FailedErrorImportCancellationTests : PersistenceTestBase
{
    [Test, CancelAfter(60_000)]
    public async Task Replay_stops_at_the_message_that_cancels()
    {
        await StoreImport("native-1");
        await StoreImport("native-2");
        await StoreImport("native-3");
        await CompleteDatabaseOperation();

        using var source = new CancellationTokenSource();
        var replayed = new List<string>();

        Assert.That(
            async () => await FailedImportStore.ProcessFailedErrorImports(
                (message, _) =>
                {
                    replayed.Add(message.Id);
                    source.Cancel();
                    return Task.CompletedTask;
                },
                source.Token),
            Throws.InstanceOf<OperationCanceledException>(),
            "an interrupted replay must not return as though it had completed");

        await CompleteDatabaseOperation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Has.Count.EqualTo(1), "the loop should stop once the token is cancelled");
            Assert.That(
                await FailedImportStore.QueryContainsFailedImports(),
                Is.True,
                "the imports that were never replayed must still be pending");
        }
    }

    [Test, CancelAfter(60_000)]
    public async Task Replay_throws_when_the_token_is_already_cancelled()
    {
        await StoreImport("native-1");
        await CompleteDatabaseOperation();

        var replayed = new List<string>();

        Assert.That(
            async () => await FailedImportStore.ProcessFailedErrorImports(
                (message, _) =>
                {
                    replayed.Add(message.Id);
                    return Task.CompletedTask;
                },
                new CancellationToken(true)),
            Throws.InstanceOf<OperationCanceledException>());

        await CompleteDatabaseOperation();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replayed, Is.Empty, "nothing should be replayed under a cancelled token");
            Assert.That(
                await FailedImportStore.QueryContainsFailedImports(),
                Is.True,
                "the pending import must survive so a later run can replay it");
        }
    }

    Task StoreImport(string nativeId)
    {
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = nativeId,
            [Headers.ProcessingEndpoint] = "Sales",
            ["NServiceBus.ExceptionInfo.ExceptionType"] = "System.InvalidOperationException",
            ["NServiceBus.ExceptionInfo.Message"] = "Something went wrong"
        };

        var failure = new FailedErrorImport
        {
            Id = FailedErrorImport.DeriveKey(headers, nativeId).ToString(),
            Message = new FailedTransportMessage
            {
                Id = nativeId,
                Headers = headers,
                Body = Encoding.UTF8.GetBytes("<order>1</order>")
            },
            ExceptionInfo = "Import failed"
        };

        return FailedImportStore.StoreFailedErrorImport(failure);
    }
}
