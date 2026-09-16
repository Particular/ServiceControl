#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Diagnostics;
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
using Raven.Client.Exceptions.Security;
using ServiceControl.Configuration;
using ServiceControl.RavenDB;

sealed class RavenReadOnlySourceLifecycle(RavenPersisterSettings settings, SettingsRootNamespace settingsRoot) : IAsyncDisposable
{
    public RavenPersisterSettings Settings => settings;

    public SettingsRootNamespace SettingsRoot => settingsRoot;

    public IDocumentStore DocumentStore => documentStore ?? throw new InvalidOperationException($"The migration source is not open. Call {nameof(Open)} first.");

    public async Task Open(CancellationToken cancellationToken = default)
    {
        if (documentStore is not null)
        {
            throw new InvalidOperationException("The migration source is already open. Opening it twice would abandon the first server without stopping it.");
        }

        try
        {
            var serverUrl = settings.UseEmbeddedServer ? await StartEmbedded(cancellationToken) : settings.ConnectionString;
            documentStore = Connect(serverUrl);

            if (!settings.UseEmbeddedServer)
            {
                // Only an external server can be older than the client; an embedded one ships beside it.
                await StartupChecks.EnsureServerVersion(documentStore, cancellationToken);
            }

            await EnsureReadable(settings.DatabaseName, $"{settingsRoot}/{RavenBootstrapper.DatabaseNameKey}", cancellationToken);
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
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await DocumentStore.Maintenance.ForDatabase(databaseName).SendAsync(new GetStatisticsOperation(), cancellationToken);
                return;
            }
            // A large embedded database routinely exceeds the load timeout on first open, which
            // RavenEmbeddedPersistenceLifecycle already allows for the same way. A locked or corrupt data
            // directory never loads at all, so the budget is what stops that becoming a silent hang.
            catch (DatabaseLoadTimeoutException e) when (settings.UseEmbeddedServer && elapsed.Elapsed >= EmbeddedLoadBudget)
            {
                throw new InvalidOperationException($"The RavenDB migration source at {Located()} has a database named '{databaseName}', from the '{settingKey}' setting, but it did not finish loading within {EmbeddedLoadBudget.TotalMinutes:N0} minutes. A data directory held by another process, or one that is corrupt, is the usual cause.", e);
            }
            catch (DatabaseLoadTimeoutException) when (settings.UseEmbeddedServer)
            {
                await Task.Delay(EmbeddedLoadRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DatabaseDoesNotExistException e)
            {
                throw new InvalidOperationException($"The RavenDB migration source at {Located()} has no database named '{databaseName}'. That name comes from the '{settingKey}' setting. Correct it before migrating: a wrong name reads a database that is not there rather than the one that is.", e);
            }
            catch (AuthorizationException e)
            {
                throw new InvalidOperationException($"The RavenDB migration source at {Located()} refused its client certificate access to the database '{databaseName}'. Grant that certificate Read access to '{databaseName}', or supply one that has it in '{settingsRoot}/{RavenBootstrapper.ClientCertificateBase64Key}' or '{settingsRoot}/{RavenBootstrapper.ClientCertificatePathKey}'. If '{databaseName}' is the wrong name, correct the '{settingKey}' setting instead: RavenDB refuses a certificate that has no access to a database whether or not that database exists.", e);
            }
            catch (Exception e) when (e is not DatabaseLoadTimeoutException)
            {
                throw new InvalidOperationException($"The RavenDB migration source at {Located()} has a database named '{databaseName}', from the '{settingKey}' setting, but could not load it.", e);
            }
        }
    }

    string Located() => Located(settings, settingsRoot);

    internal static string Located(RavenPersisterSettings sourceSettings, SettingsRootNamespace root) => sourceSettings.UseEmbeddedServer
        ? $"{sourceSettings.ServerUrl} (embedded, data directory '{sourceSettings.DatabasePath}', from '{root}/{RavenBootstrapper.DatabasePathKey}')"
        : sourceSettings.ConnectionString;

    async Task<string> StartEmbedded(CancellationToken cancellationToken)
    {
        // A dynamic query is a POST to /queries, which the request guard allows and which builds an auto-index
        // on the customer's fallback database. This makes the server refuse it rather than trusting every reader.
        var configuration = new EmbeddedDatabaseConfiguration(settings.ServerUrl, settings.DatabaseName, settings.DatabasePath, settings.LogPath, settings.LogsMode) { DisableAutoIndexCreation = true };

        embedded = EmbeddedDatabase.Start(configuration, lifetime);

        try
        {
            return await embedded.WaitUntilReady(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"The RavenDB migration source could not start a server for the embedded database at {Located()}. A ServiceControl instance still running against that data directory is the usual cause: stop it, run the report, then start it again.", e);
        }
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
            return !path.Contains(HiLoPathSegment, StringComparison.OrdinalIgnoreCase);
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

    const string HiLoPathSegment = "/hilo/";
    static readonly string[] ReadOnlyPostPaths = ["/queries", "/multi_get", "/streams/queries"];
    static readonly TimeSpan EmbeddedShutdownTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan EmbeddedLoadRetryDelay = TimeSpan.FromMilliseconds(500);
    // Generous because one DatabaseLoadTimeoutException already means RavenDB waited its own load timeout.
    static readonly TimeSpan EmbeddedLoadBudget = TimeSpan.FromMinutes(5);

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
