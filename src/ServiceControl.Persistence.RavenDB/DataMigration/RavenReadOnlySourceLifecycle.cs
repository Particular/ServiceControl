#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide.Operations;
using ServiceControl.RavenDB;

sealed class RavenReadOnlySourceLifecycle(RavenPersisterSettings settings) : IAsyncDisposable
{
    public RavenPersisterSettings Settings => settings;

    public IDocumentStore DocumentStore => documentStore ?? throw new InvalidOperationException($"The migration source is not open. Call {nameof(Open)} first.");

    public async Task Open(CancellationToken cancellationToken = default)
    {
        if (documentStore is not null)
        {
            throw new InvalidOperationException("The migration source is already open. Opening it twice would abandon the first server without stopping it.");
        }

        try
        {
            var serverUrl = settings.UseEmbeddedServer ? StartEmbedded() : settings.ConnectionString;
            documentStore = Connect(serverUrl);

            if (!settings.UseEmbeddedServer)
            {
                // Only an external server can be older than the client; an embedded one ships beside it.
                await StartupChecks.EnsureServerVersion(documentStore, cancellationToken);
            }

            // The persister cannot reach the host's Settings class for the root namespace, so it is spelled
            // out here: a customer reads these two keys back out of app.config, not out of code.
            await EnsureReadable(settings.DatabaseName, $"ServiceControl/{RavenBootstrapper.DatabaseNameKey}", cancellationToken);
            await EnsureReadable(settings.ThroughputDatabaseName, $"{ThroughputSettings.SettingsNamespace}/{ThroughputSettings.DatabaseNameKey}", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync();
            throw;
        }
        catch (Exception)
        {
            await DisposeAsync();
            throw;
        }
    }

    public IAsyncDocumentSession OpenSession(string databaseName) =>
        DocumentStore.OpenAsyncSession(new SessionOptions { Database = databaseName, NoTracking = true });

    async Task EnsureReadable(string databaseName, string settingKey, CancellationToken cancellationToken)
    {
        var record = await DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken);

        if (record is null)
        {
            throw new InvalidOperationException($"The RavenDB migration source at {Located()} has no database named '{databaseName}'. That name comes from the '{settingKey}' setting. Correct it before migrating: a wrong name reads a database that is not there rather than the one that is.");
        }

        await LoadDatabase(databaseName, settingKey, cancellationToken);
    }

    async Task LoadDatabase(string databaseName, string settingKey, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetStatisticsOperation(), cancellationToken);
                return;
            }
            catch (DatabaseLoadTimeoutException) when (settings.UseEmbeddedServer)
            {
                // A large embedded database routinely exceeds the load timeout on first open, which
                // RavenEmbeddedPersistenceLifecycle already allows for the same way.
                await Task.Delay(EmbeddedLoadRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not DatabaseLoadTimeoutException)
            {
                throw new InvalidOperationException($"The RavenDB migration source at {Located()} has a database named '{databaseName}', from the '{settingKey}' setting, but could not load it.", e);
            }
        }
    }

    string Located() => settings.UseEmbeddedServer
        ? $"{settings.ServerUrl} (embedded, data directory '{settings.DatabasePath}', from 'ServiceControl/DBPath')"
        : settings.ConnectionString;

    string StartEmbedded()
    {
        var configuration = new EmbeddedDatabaseConfiguration(settings.ServerUrl, settings.DatabaseName, settings.DatabasePath, settings.LogPath, settings.LogsMode);

        embedded = EmbeddedDatabase.Start(configuration, lifetime);

        return embedded.ServerUrl;
    }

    IDocumentStore Connect(string serverUrl)
    {
        var store = new DocumentStore
        {
            Database = settings.DatabaseName,
            Urls = [serverUrl],
            Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
        };

        if (!settings.UseEmbeddedServer)
        {
            store.Certificate = RavenClientCertificate.FindClientCertificate(settings);
        }

        store.OnBeforeRequest += RefuseWrite;

        return store.Initialize();
    }

    static void RefuseWrite(object? sender, BeforeRequestEventArgs e)
    {
        if (IsRead(e.Request.Method, new Uri(e.Url).AbsolutePath))
        {
            return;
        }

        throw new InvalidOperationException($"The RavenDB migration source is open read-only and refused a {e.Request.Method} to '{e.Url}'. Nothing in a migration may write to the database it is reading.");
    }

    static bool IsRead(HttpMethod method, string path)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Head)
        {
            // HiLo persists the id range it hands out, so it writes despite being a GET.
            return !path.Contains("/hilo/", StringComparison.OrdinalIgnoreCase);
        }

        return method == HttpMethod.Post && Array.Exists(ReadOnlyPostPaths, suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask DisposeAsync()
    {
        documentStore?.Dispose();
        documentStore = null;

        if (embedded is not null)
        {
            // Stop force-kills only once this token cancels; EmbeddedDatabase sets the graceful wait to an hour.
            using var shutdown = new CancellationTokenSource(EmbeddedShutdownTimeout);
            await embedded.Stop(shutdown.Token);
            embedded.Dispose();
            embedded = null;
        }
    }

    static readonly string[] ReadOnlyPostPaths = ["/queries", "/multi_get", "/streams/queries"];
    static readonly TimeSpan EmbeddedShutdownTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan EmbeddedLoadRetryDelay = TimeSpan.FromMilliseconds(500);

    IDocumentStore? documentStore;
    EmbeddedDatabase? embedded;
    readonly SourceLifetime lifetime = new();

    sealed class SourceLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
