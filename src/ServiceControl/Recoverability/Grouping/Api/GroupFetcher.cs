namespace ServiceControl.Recoverability.Grouping.Api
{
    using System.Collections.Generic;
    using Raven.Client;
    using System.Linq;
    using System;

    public class GroupFetcher
    {
        private readonly OperationManager operationManager;

        public GroupFetcher(OperationManager operationManager)
        {
            this.operationManager = operationManager;
        }

        public GroupOperation[] GetGroups(IDocumentSession session, string classifier)
        {
            var dbGroups = GetDBGroups(classifier, session);

            var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();
            var unacknowledgedRetries = retryHistory.GetUnacknowledgedByClassifier(classifier);

            var openRetryAcknowledgements = MapAcksToOpenGroups(dbGroups, unacknowledgedRetries);
            var closedRetryAcknowledgements = unacknowledgedRetries.Except(openRetryAcknowledgements).ToArray();
            var closedGroups = MapClosedGroups(classifier, closedRetryAcknowledgements);
            var openGroups = MapOpenGroups(dbGroups, retryHistory, openRetryAcknowledgements).ToList();
            openGroups = MapOpenGroups(openGroups, operationManager.GetArchivalOperations()).ToList();

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

            var fg = MapOpenForForwardingOperation(classifier, forwardingBatch, operationManager.GetStatusForRetryOperation(forwardingBatch.RequestId, RetryType.FailureGroup));
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

        private static GroupOperation MapOpenForForwardingOperation(string classifier, RetryBatch forwardingBatch, RetryOperation summary)
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
                var unacknowledged = standaloneUnacknowledgements.First(unack => unack.RequestId == standalone.RequestId && (RetryType)unack.OperationType == RetryType.FailureGroup);

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

        private IEnumerable<GroupOperation> MapOpenGroups(IEnumerable<GroupOperation> openGroups, IEnumerable<ArchiveOperationLogic> archiveOperations)
        {
            foreach (var group in openGroups)
            {
                var matchingArchive = archiveOperations.FirstOrDefault(op => op.RequestId == group.Id);

                if (matchingArchive != null)
                {
                    group.OperationStatus = matchingArchive.ArchiveState.ToString() ?? "None";
                    group.OperationFailed = false;
                    group.OperationProgress = matchingArchive?.GetProgress().Percentage ?? 0.0;
                    group.OperationRemainingCount = matchingArchive?.GetProgress().MessagesRemaining;
                    group.OperationStartTime = matchingArchive?.Started;
                    group.OperationCompletionTime = matchingArchive?.CompletionTime;
                    group.NeedUserAcknowledgement = false;
                }

                yield return group;
            }
        }

        private IEnumerable<GroupOperation> MapOpenGroups(IEnumerable<FailureGroupView> activeGroups, RetryHistory history, UnacknowledgedRetryOperation[] groupUnacknowledgements)
        {
            return activeGroups.Select(failureGroup =>
            {
                var summary = operationManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);
                var historic = GetLatestHistoricOperation(history, failureGroup.Id, RetryType.FailureGroup);
                var unacknowledged = groupUnacknowledgements.FirstOrDefault(unack => unack.RequestId == failureGroup.Id && (RetryType)unack.OperationType == RetryType.FailureGroup);

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