using System.Collections.Generic;
using System.Reflection;

namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;

    public class EmbeddedDatabase : IDisposable
    {
        readonly int expirationProcessTimerInSeconds;
        readonly Dictionary<string, IDocumentStore> preparedDocumentStores = new Dictionary<string, IDocumentStore>();

        public EmbeddedDatabase(int expirationProcessTimerInSeconds)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
        }


        public static EmbeddedDatabase Start(string dbPath, string logPath, int expirationProcessTimerInSecond)
        {
            var watch = new Stopwatch();
            watch.Start();
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = dbPath,
                LogsPath = logPath,
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
            watch.Stop();
            Console.WriteLine($"EmbeddedDatabase::Start took {watch.ElapsedMilliseconds} ms");

            return new EmbeddedDatabase(expirationProcessTimerInSecond);
        }

        public async Task<IDocumentStore> PrepareDatabase(string name, params Assembly[] indexAssemblies)
        {
            if (!preparedDocumentStores.TryGetValue(name, out var store))
            {
                store = await InitializeDatabase(name, indexAssemblies).ConfigureAwait(false);
                preparedDocumentStores[name] = store;
            }
            return store;
        }

        async Task<IDocumentStore> InitializeDatabase(string name, Assembly[] indexAssemblies)
        {
            var watch = new Stopwatch();
            watch.Start();
            var dbOptions = new DatabaseOptions(name)
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            var documentStore =
                await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions).ConfigureAwait(false);

            foreach (var indexAssembly in indexAssemblies)
            {
                await IndexCreation.CreateIndexesAsync(indexAssembly, documentStore).ConfigureAwait(false);
            }

            // TODO: Check to see if the configuration has changed.
            // If it has, then send an update to the server to change the expires metadata on all documents
            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = expirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig))
                .ConfigureAwait(false);

            watch.Stop();
            Console.WriteLine($"EmbeddedDatabase::PrepareDatabase took {watch.ElapsedMilliseconds} ms");

            return documentStore;
        }

        public void Dispose()
        {
            foreach (var store in preparedDocumentStores.Values)
            {
                store.Dispose();
            }
            EmbeddedServer.Instance.Dispose();
        }
    }
}