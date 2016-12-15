namespace ServiceControl.Recoverability.Grouping.Api
{
    using System.Collections.Generic;
    using Raven.Client;
    using System.Linq;

    public class GroupFetcher
    {
        private readonly RetryOperationManager retryOperationManager;

        public GroupFetcher(RetryOperationManager retryOperationManager)
        {
            this.retryOperationManager = retryOperationManager;
        }

        public RetryGroup[] GetGroups(IDocumentSession session, string classifier)
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

        private void MakeSureForwardingBatchIsIncludedAsOpen(string classifier, RetryBatch forwardingBatch, List<RetryGroup> open)
        {
            if (forwardingBatch == null || forwardingBatch.Classifier != classifier)
            {
                return;
            }

            if (IsCurrentForwardingOperationIncluded(open, forwardingBatch))
            {
                return;
            }

            var fg = MapOpenForForwardingOperation(classifier, forwardingBatch, retryOperationManager.GetStatusForRetryOperation(forwardingBatch.RequestId, RetryType.FailureGroup));
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

        private static bool IsCurrentForwardingOperationIncluded(List<RetryGroup> open, RetryBatch forwardingBatch)
        {
            return open.Any(x => x.Id == forwardingBatch.RequestId && x.Type == forwardingBatch.Classifier && forwardingBatch.RetryType == RetryType.FailureGroup);
        }

        private static RetryGroup MapOpenForForwardingOperation(string classifier, RetryBatch forwardingBatch, RetryOperation summary)
        {
            var progress = summary.GetProgress();
            return new RetryGroup
            {
                Id = forwardingBatch.RequestId,
                Title = forwardingBatch.Originator,
                Type = classifier,
                Count = 0,
                Last = summary.Last,
                RetryStatus = summary.RetryState.ToString(),
                RetryFailed = summary.Failed,
                RetryProgress = progress.Percentage,
                RetryRemainingCount = progress.MessagesRemaining,
                RetryCompletionTime = summary.CompletionTime,
                RetryStartTime = summary.Started,
                NeedUserAcknowledgement = false
            };
        }

        private static RetryBatch GetCurrentForwardingBatch(IDocumentSession session)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            return nowForwarding == null ? null : session.Load<RetryBatch>(nowForwarding.RetryBatchId);
        }

        private static IEnumerable<RetryGroup> MapClosedGroups(string classifier, UnacknowledgedRetryOperation[] standaloneUnacknowledgements)
        {
            return standaloneUnacknowledgements.Select(standalone =>
            {
                var unacknowledged = standaloneUnacknowledgements.First(unack => unack.RequestId == standalone.RequestId && unack.RetryType == RetryType.FailureGroup);

                return new RetryGroup
                {
                    Id = unacknowledged.RequestId,
                    Title = unacknowledged.Originator,
                    Type = classifier,
                    Count = unacknowledged.NumberOfMessagesProcessed,
                    Last = unacknowledged.Last,
                    RetryStatus = RetryState.Completed.ToString(),
                    RetryFailed = unacknowledged.Failed,
                    RetryCompletionTime = unacknowledged.CompletionTime,
                    RetryStartTime = unacknowledged.StartTime,
                    NeedUserAcknowledgement = true
                };
            });
        }

        private IEnumerable<RetryGroup> MapOpenGroups(IEnumerable<FailureGroupView> activeGroups, RetryHistory history, UnacknowledgedRetryOperation[] groupUnacknowledgements)
        {
            return activeGroups.Select(failureGroup =>
            {
                var summary = retryOperationManager.GetStatusForRetryOperation(failureGroup.Id, RetryType.FailureGroup);
                var historic = GetLatestHistoricOperation(history, failureGroup.Id, RetryType.FailureGroup);
                var unacknowledged = groupUnacknowledgements.FirstOrDefault(unack => unack.RequestId == failureGroup.Id && unack.RetryType == RetryType.FailureGroup);

                return new RetryGroup
                {
                    Id = failureGroup.Id,
                    Title = failureGroup.Title,
                    Type = failureGroup.Type,
                    Count = failureGroup.Count,
                    First = failureGroup.First,
                    Last = failureGroup.Last,
                    RetryStatus = summary?.RetryState.ToString() ?? "None",
                    RetryFailed = summary?.Failed,
                    RetryProgress = summary?.GetProgress().Percentage ?? 0.0,
                    RetryRemainingCount = summary?.GetProgress().MessagesRemaining,
                    RetryStartTime = summary?.Started,
                    RetryCompletionTime = summary?.CompletionTime ?? (unacknowledged?.CompletionTime ?? historic?.CompletionTime),
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
