#nullable enable

namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Queries;
    using Raven.Client.Documents.Session;
    using ServiceControl.Audit.Persistence.RavenDB;

    /// <summary>
    /// Test seam for the bucket-mode read path with two independent, timing-free capabilities:
    ///
    ///  1. Gate: when armed, every OpenSession call blocks until the test releases the gate, and the
    ///     second concurrent OpenSession call completes a signal the test can await. A sequential
    ///     implementation only ever has one OpenSession call in flight, so the signal never fires and
    ///     the test times out. This proves bounded concurrent fan-out.
    ///  2. Probe: every query executed through a session opened here is recorded together with the
    ///     number of materialized entities, so tests can prove the bucket message path reads only
    ///     Offset + PageSize candidates per bucket (never the whole matching set) without timing
    ///     assertions.
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

        public QueryProbe Probe { get; } = new();

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
            var session = documentStore.OpenAsyncSession(options ?? new SessionOptions());

            // The query/conversion events are only exposed on the concrete session type, not on
            // IAsyncDocumentSession; AsyncDocumentSession is the sole public implementation.
            if (session is AsyncDocumentSession asyncSession)
            {
                asyncSession.OnBeforeQuery += (_, args) =>
                    args.QueryCustomization.BeforeQueryExecuted(Probe.RecordQuery);
                asyncSession.OnAfterConversionToEntity += (_, _) => Probe.RecordEntity();
            }

            return session;
        }

        IRavenDocumentStoreProvider? documentStoreProvider;
        bool armed;
        int openCalls;
        TaskCompletionSource releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource twoSessionsRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Thread-safe recorder for the queries and entity conversions observed through a
    /// <see cref="GatedSessionProvider"/>.
    /// </summary>
    class QueryProbe
    {
        // Raven RQL encodes paging inline, e.g. "... order by TimeSent desc skip 0 take 50".
        static readonly Regex SkipRegex = new(@"\bskip\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex TakeRegex = new(@"\btake\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly List<RecordedQuery> queries = [];
        int entities;

        public void RecordQuery(IndexQuery indexQuery)
        {
            var skip = SkipRegex.Match(indexQuery.Query).Groups[1].Value;
            var take = TakeRegex.Match(indexQuery.Query).Groups[1].Value;

            // A missing skip/take token means the probe could not bound the query; treat it as
            // unbounded so bounded-read assertions fail loudly instead of silently passing.
            var recorded = new RecordedQuery(
                indexQuery.Query,
                skip.Length > 0 ? int.Parse(skip) : int.MaxValue,
                take.Length > 0 ? int.Parse(take) : int.MaxValue);

            lock (queries)
            {
                queries.Add(recorded);
            }
        }

        public void RecordEntity() => Interlocked.Increment(ref entities);

        public IReadOnlyList<RecordedQuery> Queries
        {
            get
            {
                lock (queries)
                {
                    return queries.ToArray();
                }
            }
        }

        public int MaxSkipPlusTake => Queries.Count == 0 ? 0 : Queries.Max(q => q.Skip + q.Take);

        public int EntitiesConverted => Volatile.Read(ref entities);

        public void Reset()
        {
            lock (queries)
            {
                queries.Clear();
            }
            Interlocked.Exchange(ref entities, 0);
        }

        public sealed record RecordedQuery(string Rql, int Skip, int Take);
    }
}
