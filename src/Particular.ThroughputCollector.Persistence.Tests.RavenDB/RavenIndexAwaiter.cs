using System;
using System.Threading;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;

public static class RavenIndexAwaiter
{
    public static void WaitForIndexing(this IDocumentStore store)
    {
        store.WaitForIndexing(10);
    }

    static void WaitForIndexing(this IDocumentStore store, int secondsToWait)
    {
        Assert.True(SpinWait.SpinUntil(() => store.Maintenance.Send(new GetStatisticsOperation()).StaleIndexes.Length == 0, TimeSpan.FromSeconds(secondsToWait)));
    }
}