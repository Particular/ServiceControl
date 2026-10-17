namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using FileSystem;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using TestHelper;

    [TestFixture]
    [Platform("Win")]
    [NonParallelizable]
    class PackagedRavenDBTests
    {
        [Test]
        public async Task CanPersistAndIndexDocumentsWithoutNativeKafkaDependencies()
        {
            var root = Path.Combine(Path.GetTempPath(), "PackagedRavenDB", Guid.NewGuid().ToString("N"));
            var serverDirectory = Path.Combine(root, "Server");
            var dataDirectory = Path.Combine(root, "Data");
            var serverUrl = $"http://127.0.0.1:{PortUtility.FindAvailablePort(33400)}";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CurrentContext.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var cancellationToken = timeout.Token;
            Process server = null;
            Task<string> standardOutput = null;
            Task<string> standardError = null;

            try
            {
                FileUtils.UnzipToSubdirectory("RavenDBServer.zip", serverDirectory);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(Path.Combine(serverDirectory, "libcurl.dll"), Does.Not.Exist);
                    Assert.That(Path.Combine(serverDirectory, "librdkafka.dll"), Does.Not.Exist);
                    Assert.That(Path.Combine(serverDirectory, "librdkafkacpp.dll"), Does.Not.Exist);
                    Assert.That(Path.Combine(serverDirectory, "Raven.Server.exe"), Does.Exist);
                }

                await StartServer();

                const string databaseName = "PackagingSmokeTest";
                using var store = new DocumentStore
                {
                    Urls = [serverUrl],
                    Database = databaseName,
                    Conventions = { DisableTopologyUpdates = true }
                }.Initialize();

                await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)), cancellationToken);
                await store.Maintenance.SendAsync(new PutIndexesOperation(new IndexDefinition
                {
                    Name = indexName,
                    Maps = ["from document in docs select new { document.Name }"]
                }), cancellationToken);

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new PackagedDocument { Name = documentName }, documentId, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);
                }

                await AssertDocumentAndIndex(store, cancellationToken);
                await StopServer();
                await StartServer();
                await AssertDocumentAndIndex(store, cancellationToken);
            }
            finally
            {
                await StopServer();
                if (Directory.Exists(root))
                {
                    FileUtils.DeleteDirectory(root, recursive: true, contentsOnly: false);
                }
            }

            async Task StartServer()
            {
                var startInfo = new ProcessStartInfo(Path.Combine(serverDirectory, "Raven.Server.exe"))
                {
                    WorkingDirectory = serverDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--non-interactive");
                startInfo.ArgumentList.Add("--log-to-console");
                startInfo.ArgumentList.Add($"--ServerUrl={serverUrl}");
                startInfo.ArgumentList.Add($"--DataDir={dataDirectory}");
                startInfo.ArgumentList.Add($"--Logs.Path={Path.Combine(root, "Logs")}");
                startInfo.ArgumentList.Add("--Setup.Mode=None");
                startInfo.ArgumentList.Add("--Security.UnsecuredAccessAllowed=Local");
                startInfo.ArgumentList.Add("--License.Eula.Accepted=true");
                startInfo.ArgumentList.Add($"--License.Path={Path.Combine(TestContext.CurrentContext.TestDirectory, "RavenLicense.json")}");
                startInfo.ArgumentList.Add("--License.DisableAutoUpdate=true");
                startInfo.ArgumentList.Add("--License.DisableLicenseSupportCheck=true");

                server = Process.Start(startInfo);
                standardOutput = server.StandardOutput.ReadToEndAsync(CancellationToken.None);
                standardError = server.StandardError.ReadToEndAsync(CancellationToken.None);

                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Assert.That(server.HasExited, Is.False, "The packaged RavenDB server exited before accepting requests.");
                    try
                    {
                        using var response = await client.GetAsync($"{serverUrl}/build/version", cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            return;
                        }
                    }
                    catch (HttpRequestException)
                    {
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            async Task StopServer()
            {
                if (server == null)
                {
                    return;
                }

                try
                {
                    if (!server.HasExited)
                    {
                        server.Kill(entireProcessTree: true);
                    }

                    await server.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));
                    TestContext.Out.WriteLine(await standardOutput.WaitAsync(TimeSpan.FromSeconds(15)));
                    TestContext.Out.WriteLine(await standardError.WaitAsync(TimeSpan.FromSeconds(15)));
                }
                finally
                {
                    server.Dispose();
                    server = null;
                }
            }
        }

        static async Task AssertDocumentAndIndex(IDocumentStore store, CancellationToken cancellationToken)
        {
            using var session = store.OpenAsyncSession();
            var document = await session.LoadAsync<PackagedDocument>(documentId, cancellationToken);
            Assert.That(document?.Name, Is.EqualTo(documentName));

            var indexedDocument = await session.Advanced.AsyncDocumentQuery<PackagedDocument>(indexName)
                .WaitForNonStaleResults(TimeSpan.FromSeconds(30))
                .WhereEquals(nameof(PackagedDocument.Name), documentName)
                .SingleAsync(cancellationToken);
            Assert.That(indexedDocument.Name, Is.EqualTo(documentName));
        }

        class PackagedDocument
        {
            public string Name { get; set; }
        }

        const string documentId = "documents/1";
        const string documentName = "Packaged RavenDB";
        const string indexName = "Packaging/ByName";
    }
}
