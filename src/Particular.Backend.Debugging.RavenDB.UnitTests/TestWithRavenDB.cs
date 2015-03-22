namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Database.Server;
    using Raven.Json.Linq;

    [TestFixture]
    public abstract class TestWithRavenDB
    {
        public static void WaitForIndexing(IDocumentStore store, string db = null, TimeSpan? timeout = null)
        {
            var databaseCommands = store.DatabaseCommands;
            if (db != null)
                databaseCommands = databaseCommands.ForDatabase(db);
            Assert.True(SpinWait.SpinUntil(() => databaseCommands.GetStatistics().StaleIndexes.Length == 0, timeout ?? TimeSpan.FromSeconds(10)));
        }

        public static void WaitForUserToContinueTheTest(EmbeddableDocumentStore documentStore, bool debug = true)
        {
            if (debug && Debugger.IsAttached == false)
                return;

            documentStore.SetStudioConfigToAllowSingleDb();

            documentStore.DatabaseCommands.Put("Pls Delete Me", null,

                                               RavenJObject.FromObject(new { StackTrace = new StackTrace(true) }),
                                               new RavenJObject());

            documentStore.Configuration.AnonymousUserAccessMode = AnonymousUserAccessMode.Admin;
            using (var server = new HttpServer(documentStore.Configuration, documentStore.DocumentDatabase))
            {
                server.StartListening();
                Process.Start(documentStore.Configuration.ServerUrl); // start the server

                do
                {
                    Thread.Sleep(100);
                } while (documentStore.DatabaseCommands.Get("Pls Delete Me") != null && (debug == false || Debugger.IsAttached));
            }
        }
    }
}
