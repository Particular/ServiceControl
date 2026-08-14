#nullable enable

namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;
    using ServiceControl.Audit.Persistence.RavenDB;

    /// <summary>
    /// Test seam that proves bucket fan-out is concurrent without relying on timing. When armed, every
    /// OpenSession call blocks until the test releases the gate, and the second concurrent OpenSession
    /// call completes a signal the test can await. A sequential implementation only ever has one
    /// OpenSession call in flight, so the signal never fires and the test times out.
    /// </summary>
    class GatedSessionProvider : IRavenSessionProvider
    {
        public void Initialize(IRavenDocumentStoreProvider documentStoreProvider) => this.documentStoreProvider = documentStoreProvider;

        public void Arm()
        {
            armed = true;
            openCalls = 0;
            releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            twoSessionsRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WhenTwoSessionsRequested => twoSessionsRequested.Task;

        public void Release() => releaseGate.TrySetResult();

        public async ValueTask<IAsyncDocumentSession> OpenSession(SessionOptions? options = default, CancellationToken cancellationToken = default)
        {
            if (armed && Interlocked.Increment(ref openCalls) == 2)
            {
                twoSessionsRequested.TrySetResult();
            }

            if (armed)
            {
                await releaseGate.Task.WaitAsync(cancellationToken);
            }

            // Initialize is invoked by the DI factory before any session is opened.
            var documentStore = await documentStoreProvider!.GetDocumentStore(cancellationToken);
            return documentStore.OpenAsyncSession(options ?? new SessionOptions());
        }

        IRavenDocumentStoreProvider? documentStoreProvider;
        bool armed;
        int openCalls;
        TaskCompletionSource releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource twoSessionsRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
