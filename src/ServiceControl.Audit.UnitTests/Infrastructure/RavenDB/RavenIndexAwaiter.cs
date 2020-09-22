using System;
using System.Threading;
using NUnit.Framework;
using Raven.Client.Documents;

public static class RavenIndexAwaiter
{
    public static void WaitForIndexing(this IDocumentStore store)
    {
        // TODO: RAVEN5 - API missing
        //store.WaitForIndexing(10);
    }

    static void WaitForIndexing(this IDocumentStore store, int secondsToWait)
    {
        // TODO: RAVEN5 - API missing
        //var databaseCommands = store.DatabaseCommands;
        //Assert.True(SpinWait.SpinUntil(() => databaseCommands.GetStatistics().StaleIndexes.Length == 0, TimeSpan.FromSeconds(secondsToWait)));
    }
}