namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using Raven.Client;
    using System.Linq;

    public class GroupFetcher
    {
        private readonly RetryingManager retryingManager;
        private readonly ArchivingManager archivingManager;

        public GroupFetcher(RetryingManager retryingManager, ArchivingManager archivingManager)
        {
            this.retryingManager = retryingManager;
            this.archivingManager = archivingManager;
        }

        public GroupOperation[] GetGroups(IDocumentSession session, string classifier)
        {
            var dbGroups = GetDBGroups(classifier, session);

            var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();
            var unacknowledgedRetries = retryHistory.GetUnacknowledgedByClassifier(classifier);

            var openRetryAcknowledgements = MapAcksToOpenGroups(dbGroups, unacknowledgedRetries);
            var closedRetryAcknowledgements = unacknowledgedRetries.Except(openRetryAcknowledgements).ToArray();

            var closedGroups = MapClosedGroups(classifier, closedRetryAcknowledgements);
            closedGroups = closedGroups.Union(MapClosedGroups(classifier, archivingManager.GetArchivalOperations().Where(archiveOp => archiveOp.NeedsAcknowledgement())));

            var openGroups = MapOpenGroups(dbGroups, retryHistory, openRetryAcknowledgements).ToList();
            openGroups = MapOpenGroups(openGroups, archivingManager.GetArchivalOperations()).ToList();
            openGroups = openGroups.Where(group => !closedGroups.Any(closedGroup => closedGroup.Id == group.Id)).ToList();

            MakeSureForwardingBatchIsIncludedAsOpen(classifier, GetCurrentForwardingBatch(session), openGroups);

            var groups = openGroups.Union(closedGroups);

            return groups.OrderByDescending(g => g.Last).ToArray();
        }

        private void MakeSureForwardingBatchIsIncludedAsOpen(string classifier, RetryBatch forwardingBatch, List<GroupOperation> open)
        {
            if (forwardingBatch == null || forwardingBatch.Classifier != classifier)
            {
                return;
            }

            if (IsCurrentForwardingOperationIncluded(open, forwardingBatch))
            {
                return;
            }

            var fg = MapOpenForForwardingOperation(classifier, forwardingBatch, retryingManager.GetStatusForRetryOperation(forwardingBatch.RequestId, RetryType.FailureGroup));
            open.Add(fg);
        }

        private static UnacknowledgedRetryOperation[] MapAcksToOpenGroups(FailureGroupView[] groups, UnacknowledgedRetryOperation[] acks)
        {
            return (from g in groups
                    join unack in acks on g.Id equals unack.RequestId
                    select unack).ToArray();
        }

        private static FailureGroupView[] GetDBGroups(string classifier, IDocumentSession session)
        {
            var groups = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .Where(v => v.Type == classifier)
                .OrderByDescending(x => x.Last)
                .Take(200)
                .ToArray();
            return groups;
        }

        private static bool IsCurrentForwardingOperationIncluded(List<GroupOperation> open, RetryBatch forwardingBatch)
        {
            return open.Any(x => x.Id == forwardingBatch.RequestId && x.Type == forwardingBatch.Classifier && forwardingBatch.RetryType == RetryType.FailureGroup);
        }

        private static GroupOperation MapOpenForForwardingOperation(string classifier, RetryBatch forwardingBatch, InMemoryRetry summary)
        {
            var progress = summary.GetProgress();
            return new GroupOperation
            {
                Id = forwardingBatch.RequestId,
                Title = forwardingBatch.Originator,
                Type = classifier,
                Count = 0,
                Last = summary.Last,
                OperationStatus = summary.RetryState.ToString(),
                OperationFailed = summary.Failed,
                OperationProgress = progress.Percentage,
                OperationRemainingCount = progress.MessagesRemaining,
                OperationCompletionTime = summary.CompletionTime,
                OperationStartTime = summary.Started,
                NeedUserAcknowledgement = false
            };
        }

        private static RetryBatch GetCurrentForwardingBatch(IDocumentSession session)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            return nowForwarding == null ? null : session.Load<RetryBatch>(nowForwarding.RetryBatchId);
        }

        private static IEnumerable<GroupOperation> MapClosedGroups(string classifier, UnacknowledgedRetryOperation[] standaloneUnacknowledgements)
        {
            return standaloneUnacknowledgements.Select(standalone =>
            {
                var unacknowledged = standaloneUnacknowledgements.First(unack => unack.RequestId == standalone.RequestId && unack.RetryType == RetryType.FailureGroup);

                return new GroupOperation
                {
                    Id = unacknowledged.RequestId,
                    Title = unacknowledged.Originator,
                    Type = classifier,
                    Count = unacknowledged.NumberOfMessagesProcessed,
                    Last = unacknowledged.Last,
                    OperationStatus = RetryState.Completed.ToString(),
                    OperationFailed = unacknowledged.Failed,
                    OperationCompletionTime = unacknowledged.CompletionTime,
                    OperationStartTime = unacknowledged.StartTime,
                    NeedUserAcknowledgement = true
                };
            });
        }

        private IEnumerable<GroupOperation> MapClosedGroups(string classifier, IEnumerable<InMemoryArchive> completedArchiveOperations)
        {
            return completedArchiveOperations.Select(archiveOperation =>
            {
                return new GroupOperation
                {
                    Id = archiveOperation.RequestId,
                    Title = archiveOperation.GroupName,
                    Type = classifier,
                    Count = archiveOperation.NumberOfMessagesArchived,
                    Last = archiveOperation.Last,
                    OperationStatus = archiveOperation.ArchiveState.ToString(),
                    OperationFailed = false,
                    OperationProgress = archiveOperation.GetProgress().Percentage,
                    OperationRemainingCount = archiveOperation?.GetProgress().MessagesRemaining,
                    OperationStartTime = archiveOperation?.Started,
                    OperationCompletionTime = archiveOperation?.CompletionTime,
                    OperationMessagesCompletedCount = archiveOperation?.GetProgress().NumberOfMessagesArchived,
                    NeedUserAcknowledgement = archiveOperation.NeedsAcknowledgement(),
                };
            });
        }

        private IEnumerable<GroupOperation> MapOpenGroups(IEnumerable<GroupOperation> openGroups, IEnumerable<InMemoryArchive> archiveOperations)
        {
            foreach (var group in openGroups)
            {
                var matchingArchive = archiveOperations.FirstOrDefault(op => op.RequestId == group.Id);

                if (matchingArchive != null)
                {
                    group.OperationStatus = matchingArchive.ArchiveState.ToString();
                    group.OperationFailed = false;
                    group.OperationProgress = matchingArchive.GetProgress().Percentage;
                    group.OperationRemainingCount = matchingArchive?.GetProgress().MessagesRemaining;
                    group.OperationStartTime = matchingArchive?.Started;
                    group.OperationCompletionTime = matchingArchive?.CompletionTime;
                    group.OperationMessagesCompletedCount = matchingArchive?.GetProgress().NumberOfMessagesArchived;
                    group.NeedUserAcknowledgement = matchingArchive.NeedsAcknowledgement();
                }

                yield return group;
            }
        }

        private IEnumerable<GroupOperation> MapOpenGroups(IEnumerable<FailureGroupView> activeGroups, RetryHistory history, UnacknowledgedRetryOperation[] groupUnacknowledgements)
        {
            return activeGroups.Select(failureGroup =>
            {
                var summary = retryingManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);
                var historic = GetLatestHistoricOperation(history, failureGroup.Id, RetryType.FailureGroup);
                var unacknowledged = groupUnacknowledgements.FirstOrDefault(unack => unack.RequestId == failureGroup.Id && unack.RetryType == RetryType.FailureGroup);

                return new GroupOperation
                {
                    Id = failureGroup.Id,
                    Title = failureGroup.Title,
                    Type = failureGroup.Type,
                    Count = failureGroup.Count,
                    First = failureGroup.First,
                    Last = failureGroup.Last,
                    OperationStatus = summary?.RetryState.ToString() ?? "None",
                    OperationFailed = summary?.Failed,
                    OperationProgress = summary?.GetProgress().Percentage ?? 0.0,
                    OperationRemainingCount = summary?.GetProgress().MessagesRemaining,
                    OperationStartTime = summary?.Started,
                    OperationCompletionTime = summary?.CompletionTime ?? (unacknowledged?.CompletionTime ?? historic?.CompletionTime),
                    NeedUserAcknowledgement = unacknowledged != null
                };
            });
        }

        private static HistoricRetryOperation GetLatestHistoricOperation(RetryHistory history, string requestId, RetryType retryType)
        {
            return history.HistoricOperations
                .Where(v => v.RequestId == requestId && v.RetryType == retryType)
                .OrderByDescending(v => v.CompletionTime)
                .FirstOrDefault();
        }
    }
}