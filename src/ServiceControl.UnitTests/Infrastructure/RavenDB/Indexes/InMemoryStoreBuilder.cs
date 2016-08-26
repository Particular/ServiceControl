using System.IO;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.FileSystem;
using Raven.Database.Config;
using Raven.Database.Server;
using Raven.Server;

public class InMemoryStoreBuilder
{
    private static RavenDbServer ravenDbServer;

    static InMemoryStoreBuilder()
    {
        NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(8077);

        ravenDbServer = new RavenDbServer(new InMemoryRavenConfiguration
        {
            DefaultStorageTypeName = "voron",
            RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
            RunInMemory = true,
            Port = 8077,
            CompiledIndexCacheDirectory = Path.GetTempPath(), // RavenDB-2236
            FileSystemName = "Test"
        }.Initialize())
        {
            UseEmbeddedHttpServer = true,
        };

        ravenDbServer.Initialize();

        ravenDbServer.Url = ravenDbServer.SystemDatabase.ServerUrl;
    }

    public static IFilesStore GetFilesStore()
    {
        var storeFs = new FilesStore
        {
            Url = ravenDbServer.Url,
            DefaultFileSystem = "Test"
        };

        return storeFs.Initialize();
    }

    public static IDocumentStore GetInMemoryStore()
    {
        var store = new DocumentStore
        {
            Conventions =
            {
                SaveEnumsAsIntegers = true
            },
            Url = ravenDbServer.Url
        };
        store.Initialize();

        return store;
    }
}