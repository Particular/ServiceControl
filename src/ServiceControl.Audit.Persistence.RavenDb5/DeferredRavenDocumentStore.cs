namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.Documents.Changes;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Identity;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;
    using Raven.Client.Documents.Smuggler;
    using Raven.Client.Documents.Subscriptions;
    using Raven.Client.Documents.TimeSeries;
    using Raven.Client.Http;

    class DeferredRavenDocumentStore : IDocumentStore
    {
        IDocumentStore documentStoreImplementation;
        public void SetInnerDocumentStore(IDocumentStore documentStore) => documentStoreImplementation = documentStore;

        public void Dispose() => documentStoreImplementation.Dispose();

        public bool WasDisposed => documentStoreImplementation.WasDisposed;

        public event EventHandler AfterDispose
        {
            add => documentStoreImplementation.AfterDispose += value;
            remove => documentStoreImplementation.AfterDispose -= value;
        }

        public IDatabaseChanges Changes(string database = null) => documentStoreImplementation.Changes(database);

        public IDatabaseChanges Changes(string database, string nodeTag) => documentStoreImplementation.Changes(database, nodeTag);

        public IDisposable AggressivelyCacheFor(TimeSpan cacheDuration, string database = null) => documentStoreImplementation.AggressivelyCacheFor(cacheDuration, database);

        public IDisposable AggressivelyCacheFor(TimeSpan cacheDuration, AggressiveCacheMode mode, string database = null) => documentStoreImplementation.AggressivelyCacheFor(cacheDuration, mode, database);

        public IDisposable AggressivelyCache(string database = null) => documentStoreImplementation.AggressivelyCache(database);

        public IDisposable DisableAggressiveCaching(string database = null) => documentStoreImplementation.DisableAggressiveCaching(database);

        public IDocumentStore Initialize() => documentStoreImplementation.Initialize();

        public IAsyncDocumentSession OpenAsyncSession() => documentStoreImplementation.OpenAsyncSession();

        public IAsyncDocumentSession OpenAsyncSession(string database) => documentStoreImplementation.OpenAsyncSession(database);

        public IAsyncDocumentSession OpenAsyncSession(SessionOptions sessionOptions) => documentStoreImplementation.OpenAsyncSession(sessionOptions);

        public IDocumentSession OpenSession() => documentStoreImplementation.OpenSession();

        public IDocumentSession OpenSession(string database) => documentStoreImplementation.OpenSession(database);

        public IDocumentSession OpenSession(SessionOptions sessionOptions) => documentStoreImplementation.OpenSession(sessionOptions);

        public void ExecuteIndex(IAbstractIndexCreationTask task, string database = null) => documentStoreImplementation.ExecuteIndex(task, database);

        public void ExecuteIndexes(IEnumerable<IAbstractIndexCreationTask> tasks, string database = null) => documentStoreImplementation.ExecuteIndexes(tasks, database);

        public Task ExecuteIndexAsync(IAbstractIndexCreationTask task, string database = null,
            CancellationToken token = new CancellationToken()) =>
            documentStoreImplementation.ExecuteIndexAsync(task, database, token);

        public Task ExecuteIndexesAsync(IEnumerable<IAbstractIndexCreationTask> tasks, string database = null, CancellationToken token = new CancellationToken()) => documentStoreImplementation.ExecuteIndexesAsync(tasks, database, token);

        public BulkInsertOperation BulkInsert(string database = null, CancellationToken token = new CancellationToken()) => documentStoreImplementation.BulkInsert(database, token);

        public BulkInsertOperation BulkInsert(string database, BulkInsertOptions options,
            CancellationToken token = new CancellationToken()) =>
            documentStoreImplementation.BulkInsert(database, options, token);

        public BulkInsertOperation BulkInsert(BulkInsertOptions options, CancellationToken token = new CancellationToken()) => documentStoreImplementation.BulkInsert(options, token);

        public RequestExecutor GetRequestExecutor(string database = null) => documentStoreImplementation.GetRequestExecutor(database);

        public IDisposable SetRequestTimeout(TimeSpan timeout, string database = null) => documentStoreImplementation.SetRequestTimeout(timeout, database);

        public X509Certificate2 Certificate => documentStoreImplementation.Certificate;

        public IHiLoIdGenerator HiLoIdGenerator => documentStoreImplementation.HiLoIdGenerator;

        public string Identifier
        {
            get => documentStoreImplementation.Identifier;
            set => documentStoreImplementation.Identifier = value;
        }

        public TimeSeriesOperations TimeSeries => documentStoreImplementation.TimeSeries;

        public DocumentConventions Conventions => documentStoreImplementation.Conventions;

        public string[] Urls => documentStoreImplementation.Urls;

        public DocumentSubscriptions Subscriptions => documentStoreImplementation.Subscriptions;

        public string Database
        {
            get => documentStoreImplementation.Database;
            set => documentStoreImplementation.Database = value;
        }

        public MaintenanceOperationExecutor Maintenance => documentStoreImplementation.Maintenance;

        public OperationExecutor Operations => documentStoreImplementation.Operations;

        public DatabaseSmuggler Smuggler => documentStoreImplementation.Smuggler;

        public event EventHandler<BeforeStoreEventArgs> OnBeforeStore
        {
            add => documentStoreImplementation.OnBeforeStore += value;
            remove => documentStoreImplementation.OnBeforeStore -= value;
        }

        public event EventHandler<AfterSaveChangesEventArgs> OnAfterSaveChanges
        {
            add => documentStoreImplementation.OnAfterSaveChanges += value;
            remove => documentStoreImplementation.OnAfterSaveChanges -= value;
        }

        public event EventHandler<BeforeDeleteEventArgs> OnBeforeDelete
        {
            add => documentStoreImplementation.OnBeforeDelete += value;
            remove => documentStoreImplementation.OnBeforeDelete -= value;
        }

        public event EventHandler<BeforeQueryEventArgs> OnBeforeQuery
        {
            add => documentStoreImplementation.OnBeforeQuery += value;
            remove => documentStoreImplementation.OnBeforeQuery -= value;
        }

        public event EventHandler<SessionCreatedEventArgs> OnSessionCreated
        {
            add => documentStoreImplementation.OnSessionCreated += value;
            remove => documentStoreImplementation.OnSessionCreated -= value;
        }

        public event EventHandler<BeforeConversionToDocumentEventArgs> OnBeforeConversionToDocument
        {
            add => documentStoreImplementation.OnBeforeConversionToDocument += value;
            remove => documentStoreImplementation.OnBeforeConversionToDocument -= value;
        }

        public event EventHandler<AfterConversionToDocumentEventArgs> OnAfterConversionToDocument
        {
            add => documentStoreImplementation.OnAfterConversionToDocument += value;
            remove => documentStoreImplementation.OnAfterConversionToDocument -= value;
        }

        public event EventHandler<BeforeConversionToEntityEventArgs> OnBeforeConversionToEntity
        {
            add => documentStoreImplementation.OnBeforeConversionToEntity += value;
            remove => documentStoreImplementation.OnBeforeConversionToEntity -= value;
        }

        public event EventHandler<AfterConversionToEntityEventArgs> OnAfterConversionToEntity
        {
            add => documentStoreImplementation.OnAfterConversionToEntity += value;
            remove => documentStoreImplementation.OnAfterConversionToEntity -= value;
        }

        public event EventHandler<FailedRequestEventArgs> OnFailedRequest
        {
            add => documentStoreImplementation.OnFailedRequest += value;
            remove => documentStoreImplementation.OnFailedRequest -= value;
        }

        public event EventHandler<BeforeRequestEventArgs> OnBeforeRequest
        {
            add => documentStoreImplementation.OnBeforeRequest += value;
            remove => documentStoreImplementation.OnBeforeRequest -= value;
        }

        public event EventHandler<SucceedRequestEventArgs> OnSucceedRequest
        {
            add => documentStoreImplementation.OnSucceedRequest += value;
            remove => documentStoreImplementation.OnSucceedRequest -= value;
        }

        public event EventHandler<TopologyUpdatedEventArgs> OnTopologyUpdated
        {
            add => documentStoreImplementation.OnTopologyUpdated += value;
            remove => documentStoreImplementation.OnTopologyUpdated -= value;
        }

        public event EventHandler<SessionDisposingEventArgs> OnSessionDisposing
        {
            add => documentStoreImplementation.OnSessionDisposing += value;
            remove => documentStoreImplementation.OnSessionDisposing -= value;
        }
    }
}