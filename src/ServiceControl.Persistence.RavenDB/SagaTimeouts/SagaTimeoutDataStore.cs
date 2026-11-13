namespace ServiceControl.Persistence.RavenDB.SagaTimeouts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Queries;
    using Raven.Client.Documents.Session;
    using Raven.Client.Exceptions;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.SagaTimeouts;

    class SagaTimeoutDataStore(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager, TimeProvider timeProvider) : ISagaTimeoutDataStore
    {
        // Two concurrent reports for the same timeout are rare, so a conflict almost never repeats
        const int MaxConcurrencyRetries = 3;

        static readonly TimeSpan CompletionQueryWait = TimeSpan.FromSeconds(30);

        public async Task<SagaTimeoutApplyOutcome> Apply(SagaTimeoutChange change, CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await TryApply(change, cancellationToken);
                }
                catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                {
                }
            }
        }

        async Task<SagaTimeoutApplyOutcome> TryApply(SagaTimeoutChange change, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Advanced.UseOptimisticConcurrency = true;

            var marker = await session.LoadAsync<CompletedSaga>(CompletedSaga.MakeDocumentId(change.SagaId), cancellationToken);
            if (marker != null)
            {
                return SagaTimeoutApplyOutcome.Ignored;
            }

            var documentId = SagaTimeout.MakeDocumentId(change.TimeoutId);
            var timeout = await session.LoadAsync<SagaTimeout>(documentId, cancellationToken);
            var isNew = timeout == null;

            if (isNew)
            {
                timeout = new SagaTimeout
                {
                    Id = documentId,
                    TimeoutId = change.TimeoutId,
                    Status = SagaTimeoutStatus.Unknown,
                    Rank = -1
                };
                await session.StoreAsync(timeout, cancellationToken);
            }

            var deduplicationKey = SagaTimeoutRules.DeduplicationKey(change);
            if (timeout.Changes.Any(entry => entry.DeduplicationKey == deduplicationKey))
            {
                return SagaTimeoutApplyOutcome.Duplicate;
            }

            timeout.EndpointName ??= change.EndpointName;
            timeout.SagaType ??= change.SagaType;
            timeout.SagaId = change.SagaId;
            timeout.CorrelationValue ??= change.CorrelationValue;
            timeout.TimeoutType ??= change.TimeoutType;

            var changedAt = change.ChangedAt.UtcDateTime;
            if (timeout.LastReportedAt == null || changedAt >= timeout.LastReportedAt)
            {
                timeout.LastReportedAt = changedAt;
                timeout.Host = change.Host ?? timeout.Host;
                timeout.HostId = change.HostId ?? timeout.HostId;
                timeout.ManagementAddress = change.ManagementAddress ?? timeout.ManagementAddress;
            }

            AddHistoryEntry(timeout, change, deduplicationKey);

            if (change.Kind == SagaTimeoutChangeKind.Requested && timeout.RequestedAt == null)
            {
                timeout.RequestedAt = changedAt;
            }

            if (change.Payload != null && timeout.Payloads.All(payload => payload.Version != change.Version))
            {
                timeout.Payloads.Add(new PayloadVersion { Version = change.Version, ContentType = change.PayloadContentType });
                session.Advanced.Attachments.Store(timeout, SagaTimeout.PayloadAttachmentName(change.Version), new MemoryStream(change.Payload), change.PayloadContentType);
            }

            if (change.OperationId.HasValue && timeout.PendingOperation?.OperationId == change.OperationId)
            {
                timeout.PendingOperation = null;
            }

            var outcome = SagaTimeoutApplyOutcome.HistoryOnly;
            if (change.Kind != SagaTimeoutChangeKind.Rejected && IsNewer(change, timeout))
            {
                ApplyState(timeout, change);
                outcome = SagaTimeoutApplyOutcome.Applied;
            }

            if (timeout.Status == SagaTimeoutStatus.Pending)
            {
                expirationManager.CancelExpiration(session, timeout);
            }
            else if (isNew || outcome == SagaTimeoutApplyOutcome.Applied)
            {
                // Finished and Unknown timeouts live as long as the failed messages they can relate to
                expirationManager.EnableErrorRetentionExpiration(session, timeout);
            }

            await session.SaveChangesAsync(cancellationToken);

            return outcome;
        }

        static bool IsNewer(SagaTimeoutChange change, SagaTimeout timeout)
        {
            var rank = SagaTimeoutRules.RankOf(change.Kind);
            return change.Version > timeout.Version || (change.Version == timeout.Version && rank > timeout.Rank);
        }

        static void ApplyState(SagaTimeout timeout, SagaTimeoutChange change)
        {
            timeout.Version = change.Version;
            timeout.Rank = SagaTimeoutRules.RankOf(change.Kind);
            timeout.Status = SagaTimeoutRules.StatusAfter(change.Kind);
            timeout.TimeoutType = change.TimeoutType ?? timeout.TimeoutType;
            timeout.TriggerId = change.TriggerId ?? timeout.TriggerId;
            timeout.DueTime = change.DueTime?.UtcDateTime ?? timeout.DueTime;
            timeout.LastChangeKind = change.Kind;
            timeout.LastChangeSource = change.Source;
            timeout.LastChangedAt = change.ChangedAt.UtcDateTime;
            timeout.StatusChangedAt = change.ReceivedAt.UtcDateTime;
        }

        static void AddHistoryEntry(SagaTimeout timeout, SagaTimeoutChange change, string deduplicationKey)
        {
            timeout.Changes.Add(new HistoryEntry
            {
                DeduplicationKey = deduplicationKey,
                Kind = change.Kind,
                Version = change.Version,
                TriggerId = change.TriggerId,
                DueTime = change.DueTime?.UtcDateTime,
                ChangedAt = change.ChangedAt.UtcDateTime,
                ReceivedAt = change.ReceivedAt.UtcDateTime,
                Source = change.Source,
                OperationId = change.OperationId,
                RequestedChange = change.RequestedChange,
                RequestedDueTime = change.RequestedDueTime?.UtcDateTime,
                Reason = change.Reason
            });

            while (timeout.Changes.Count > SagaTimeoutRules.MaxHistoryEntries)
            {
                var oldest = timeout.Changes
                    .Where(entry => entry.Kind != SagaTimeoutChangeKind.Requested)
                    .OrderBy(entry => entry.ChangedAt)
                    .First();

                timeout.Changes.Remove(oldest);
                timeout.HistoryTruncated = true;
            }
        }

        public async Task<SagaTimeoutApplyOutcome> ApplySagaCompleted(SagaCompletedChange change, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var markerId = CompletedSaga.MakeDocumentId(change.SagaId);
            var marker = await session.LoadAsync<CompletedSaga>(markerId, cancellationToken);
            var outcome = SagaTimeoutApplyOutcome.Duplicate;

            if (marker == null)
            {
                marker = new CompletedSaga
                {
                    Id = markerId,
                    SagaId = change.SagaId,
                    EndpointName = change.EndpointName,
                    SagaType = change.SagaType,
                    ChangedAt = change.ChangedAt.UtcDateTime,
                    ReceivedAt = change.ReceivedAt.UtcDateTime
                };
                await session.StoreAsync(marker, cancellationToken);
                expirationManager.EnableErrorRetentionExpiration(session, marker);
                outcome = SagaTimeoutApplyOutcome.Applied;
            }

            // A redelivered completion still deletes timeouts that a report created after the first one
            var timeouts = await session.Query<SagaTimeoutsIndex.Result, SagaTimeoutsIndex>()
                .Customize(x => x.WaitForNonStaleResults(CompletionQueryWait))
                .Where(timeout => timeout.SagaId == change.SagaId)
                .OfType<SagaTimeout>()
                .ToListAsync(cancellationToken);

            foreach (var timeout in timeouts)
            {
                session.Delete(timeout);
            }

            await session.SaveChangesAsync(cancellationToken);

            return outcome;
        }

        public async Task<QueryResult<IList<SagaTimeoutView>>> Query(SagaTimeoutQuery query, PagingInfo paging, SortInfo sort, CancellationToken cancellationToken = default)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var ravenQuery = session.Query<SagaTimeoutsIndex.Result, SagaTimeoutsIndex>()
                .Statistics(out var stats)
                .FilterByStatus(query.Status, now - SagaTimeoutRules.OverdueGracePeriod);

            if (!string.IsNullOrWhiteSpace(query.EndpointName))
            {
                ravenQuery = ravenQuery.Where(timeout => timeout.EndpointName == query.EndpointName);
            }

            if (!string.IsNullOrWhiteSpace(query.SagaType))
            {
                ravenQuery = ravenQuery.Where(timeout => timeout.SagaType == query.SagaType);
            }

            if (query.SagaId.HasValue)
            {
                ravenQuery = ravenQuery.Where(timeout => timeout.SagaId == query.SagaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var terms = query.SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(term => $"*{term}*");
                ravenQuery = ravenQuery.Search(timeout => timeout.Query, string.Join(" ", terms), options: SearchOptions.And, @operator: SearchOperator.And);
            }

            var results = await Sort(ravenQuery, sort)
                .Skip(paging.Offset)
                .Take(paging.PageSize)
                .OfType<SagaTimeout>()
                .ToListAsync(cancellationToken);

            IList<SagaTimeoutView> views = results.Select(timeout => ToView(timeout, now)).ToList();
            return new QueryResult<IList<SagaTimeoutView>>(views, stats.ToQueryStatsInfo());
        }

        static IRavenQueryable<SagaTimeoutsIndex.Result> Sort(IRavenQueryable<SagaTimeoutsIndex.Result> query, SortInfo sort)
        {
            Expression<Func<SagaTimeoutsIndex.Result, object>> keySelector = sort.Sort switch
            {
                "last_changed_at" => timeout => timeout.LastChangedAt,
                "endpoint" => timeout => timeout.EndpointName,
                "saga_type" => timeout => timeout.SagaType,
                "timeout_type" => timeout => timeout.TimeoutType,
                _ => timeout => timeout.DueTime
            };

            return sort.Direction == "desc" ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
        }

        public async Task<SagaTimeoutCounts> GetCounts(CancellationToken cancellationToken = default)
        {
            var overdueCutoff = timeProvider.GetUtcNow().UtcDateTime - SagaTimeoutRules.OverdueGracePeriod;

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var pending = await session.Query<SagaTimeoutsIndex.Result, SagaTimeoutsIndex>()
                .FilterByStatus(SagaTimeoutListStatus.Pending, overdueCutoff)
                .CountAsync(cancellationToken);
            var overdue = await session.Query<SagaTimeoutsIndex.Result, SagaTimeoutsIndex>()
                .FilterByStatus(SagaTimeoutListStatus.Overdue, overdueCutoff)
                .CountAsync(cancellationToken);

            return new SagaTimeoutCounts { Pending = pending, Overdue = overdue };
        }

        public async Task<SagaTimeoutDetails> Get(string timeoutId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var documentId = SagaTimeout.MakeDocumentId(timeoutId);
            var timeout = await session.LoadAsync<SagaTimeout>(documentId, cancellationToken);
            if (timeout == null)
            {
                return null;
            }

            string payload = null;
            string payloadContentType = null;
            var currentPayload = timeout.Payloads.FirstOrDefault(p => p.Version == timeout.Version);
            if (currentPayload != null)
            {
                using var attachment = await session.Advanced.Attachments.GetAsync(documentId, SagaTimeout.PayloadAttachmentName(currentPayload.Version), cancellationToken);
                if (attachment != null)
                {
                    using var reader = new StreamReader(attachment.Stream, Encoding.UTF8);
                    payload = await reader.ReadToEndAsync(cancellationToken);
                    payloadContentType = currentPayload.ContentType;
                }
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            return new SagaTimeoutDetails
            {
                TimeoutId = timeout.TimeoutId,
                EndpointName = timeout.EndpointName,
                SagaType = timeout.SagaType,
                SagaId = timeout.SagaId,
                CorrelationValue = timeout.CorrelationValue,
                TimeoutType = timeout.TimeoutType,
                Version = timeout.Version,
                TriggerId = timeout.TriggerId,
                DueTime = timeout.DueTime,
                Status = timeout.Status,
                IsOverdue = SagaTimeoutRules.IsOverdue(timeout.Status, timeout.DueTime, now),
                RequestedAt = timeout.RequestedAt,
                LastChangeKind = timeout.LastChangeKind,
                LastChangeSource = timeout.LastChangeSource,
                LastChangedAt = timeout.LastChangedAt,
                Host = timeout.Host,
                ManagementAddress = timeout.ManagementAddress,
                PendingOperation = ToView(timeout.PendingOperation),
                PayloadContentType = payloadContentType,
                Payload = payload,
                HistoryTruncated = timeout.HistoryTruncated,
                History = timeout.Changes
                    .OrderBy(entry => entry.ChangedAt)
                    .Select(entry => new SagaTimeoutHistoryEntryView
                    {
                        Kind = entry.Kind,
                        Version = entry.Version,
                        TriggerId = entry.TriggerId,
                        DueTime = entry.DueTime,
                        ChangedAt = entry.ChangedAt,
                        ReceivedAt = entry.ReceivedAt,
                        Source = entry.Source,
                        OperationId = entry.OperationId,
                        RequestedChange = entry.RequestedChange,
                        RequestedDueTime = entry.RequestedDueTime,
                        Reason = entry.Reason
                    })
                    .ToList()
            };
        }

        public async Task<IList<SagaTimeoutView>> GetBySaga(Guid sagaId, CancellationToken cancellationToken = default)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var timeouts = await session.Query<SagaTimeoutsIndex.Result, SagaTimeoutsIndex>()
                .Where(timeout => timeout.SagaId == sagaId && timeout.Status != SagaTimeoutStatus.Unknown)
                .OrderBy(timeout => timeout.DueTime)
                .OfType<SagaTimeout>()
                .ToListAsync(cancellationToken);

            return timeouts.Select(timeout => ToView(timeout, now)).ToList();
        }

        public async Task<SagaTimeoutOperatorRequestResult> RecordOperatorRequest(SagaTimeoutOperatorRequest request, CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await TryRecordOperatorRequest(request, cancellationToken);
                }
                catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                {
                }
            }
        }

        async Task<SagaTimeoutOperatorRequestResult> TryRecordOperatorRequest(SagaTimeoutOperatorRequest request, CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Advanced.UseOptimisticConcurrency = true;

            var timeout = await session.LoadAsync<SagaTimeout>(SagaTimeout.MakeDocumentId(request.TimeoutId), cancellationToken);
            if (timeout == null)
            {
                return new SagaTimeoutOperatorRequestResult(SagaTimeoutOperatorRequestOutcome.NotFound);
            }

            var outcome = SagaTimeoutOperatorRequestOutcome.Accepted;
            if (timeout.Status != SagaTimeoutStatus.Pending)
            {
                outcome = SagaTimeoutOperatorRequestOutcome.NotPending;
            }
            else if (string.IsNullOrEmpty(timeout.ManagementAddress))
            {
                outcome = SagaTimeoutOperatorRequestOutcome.NoManagementAddress;
            }
            else if (timeout.PendingOperation != null && timeout.PendingOperation.RequestedAt > now - SagaTimeoutRules.OperatorRequestTimeout)
            {
                outcome = SagaTimeoutOperatorRequestOutcome.OperationPending;
            }
            else if (timeout.Version != request.ExpectedVersion)
            {
                outcome = SagaTimeoutOperatorRequestOutcome.VersionMismatch;
            }

            if (outcome == SagaTimeoutOperatorRequestOutcome.Accepted)
            {
                timeout.PendingOperation = new PendingOperation
                {
                    OperationId = request.OperationId,
                    Change = request.Change,
                    RequestedAt = request.RequestedAt.UtcDateTime,
                    RequestedDueTime = request.RequestedDueTime?.UtcDateTime
                };
                await session.SaveChangesAsync(cancellationToken);
            }

            return new SagaTimeoutOperatorRequestResult(outcome, ToView(timeout, now));
        }

        public async Task Forget(string timeoutId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Delete(SagaTimeout.MakeDocumentId(timeoutId));
            await session.SaveChangesAsync(cancellationToken);
        }

        static SagaTimeoutView ToView(SagaTimeout timeout, DateTime now) => new()
        {
            TimeoutId = timeout.TimeoutId,
            EndpointName = timeout.EndpointName,
            SagaType = timeout.SagaType,
            SagaId = timeout.SagaId,
            CorrelationValue = timeout.CorrelationValue,
            TimeoutType = timeout.TimeoutType,
            Version = timeout.Version,
            TriggerId = timeout.TriggerId,
            DueTime = timeout.DueTime,
            Status = timeout.Status,
            IsOverdue = SagaTimeoutRules.IsOverdue(timeout.Status, timeout.DueTime, now),
            RequestedAt = timeout.RequestedAt,
            LastChangeKind = timeout.LastChangeKind,
            LastChangeSource = timeout.LastChangeSource,
            LastChangedAt = timeout.LastChangedAt,
            Host = timeout.Host,
            ManagementAddress = timeout.ManagementAddress,
            PendingOperation = ToView(timeout.PendingOperation)
        };

        static SagaTimeoutPendingOperationView ToView(PendingOperation operation) => operation == null
            ? null
            : new SagaTimeoutPendingOperationView
            {
                OperationId = operation.OperationId,
                Change = operation.Change,
                RequestedAt = operation.RequestedAt,
                RequestedDueTime = operation.RequestedDueTime
            };
    }

    static class SagaTimeoutsQueryExtensions
    {
        public static IRavenQueryable<SagaTimeoutsIndex.Result> FilterByStatus(this IRavenQueryable<SagaTimeoutsIndex.Result> query, SagaTimeoutListStatus status, DateTime overdueCutoff) => status switch
        {
            SagaTimeoutListStatus.Overdue => query.Where(timeout => timeout.Status == SagaTimeoutStatus.Pending && timeout.DueTime < overdueCutoff),
            SagaTimeoutListStatus.All => query.Where(timeout => timeout.Status != SagaTimeoutStatus.Unknown),
            SagaTimeoutListStatus.Pending => query.Where(timeout => timeout.Status == SagaTimeoutStatus.Pending && timeout.DueTime >= overdueCutoff),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
