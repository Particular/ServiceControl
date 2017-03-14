namespace ServiceControl.Recoverability.Grouping.Api
{
    using System.Collections.Generic;
    using Raven.Client;
    using System.Linq;

    public class GroupFetcher
    {
        private readonly RetryOperationManager operationManager;

        public GroupFetcher(RetryOperationManager operationManager)
        {
            this.operationManager = operationManager;
        }

        public GroupOperation[] GetGroups(IDocumentSession session, string classifier)
        {
            var dbGroups = GetDBGroups(classifier, session);

            var history = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();
            var acks = history.GetUnacknowledgedByClassifier(classifier);

            var openAcks = MapAcksToOpenGroups(dbGroups, acks);

            var closedAcks = acks.Except(openAcks).ToArray();

            var closed = MapClosedGroups(classifier, closedAcks);

            var open = MapOpenGroups(dbGroups, history, openAcks).ToList();

            MakeSureForwardingBatchIsIncludedAsOpen(classifier, GetCurrentForwardingBatch(session), open);

            var groups = open.Union(closed);

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

        private static UnacknowledgedOperation[] MapAcksToOpenGroups(FailureGroupView[] groups, UnacknowledgedOperation[] acks)
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

        private static IEnumerable<GroupOperation> MapClosedGroups(string classifier, UnacknowledgedOperation[] standaloneUnacknowledgements)
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

        private IEnumerable<GroupOperation> MapOpenGroups(IEnumerable<FailureGroupView> activeGroups, RetryHistory history, UnacknowledgedOperation[] groupUnacknowledgements)
        {
            return activeGroups.Select(failureGroup =>
            {
                var summary = operationManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);
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