﻿using System;
using System.Threading;
using NUnit.Framework;
using Raven.Client;

namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    public static class RavenIndexAwaiter
    {
        public static void WaitForIndexing(this IDocumentStore store)
        {
            var databaseCommands = store.DatabaseCommands;
            Assert.True(SpinWait.SpinUntil(() => databaseCommands.GetStatistics().StaleIndexes.Length == 0, TimeSpan.FromSeconds(10)));
        }

    }
}