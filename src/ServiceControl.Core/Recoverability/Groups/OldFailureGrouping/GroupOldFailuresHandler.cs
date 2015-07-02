namespace ServiceControl.Recoverability.Groups.OldFailureGrouping
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability.Groups.Indexes;

    public class GroupOldFailuresHandler : IHandleMessages<GroupOldFailures>
    {
        const int BatchSize = 500;
        IBus bus;
        IDocumentSession session;
        MessageFailureHistoryGrouper grouper;
        static readonly ILog Logger = LogManager.GetLogger(typeof(GroupOldFailuresHandler));


        public GroupOldFailuresHandler(IBus bus, IDocumentSession session, MessageFailureHistoryGrouper grouper)
        {
            this.bus = bus;
            this.session = session;
            this.grouper = grouper;
        }

        public void Handle(GroupOldFailures message)
        {
            var numberOfGroupers = grouper.NumberOfAvailableGroupers();
            
            int totaltNumberOfFailuresAvailable;
            var failures = GetFailuresMissingOneOrMoreGroupers(numberOfGroupers, out totaltNumberOfFailuresAvailable);

            Logger.InfoFormat("Started grouping {0} messages failures.", failures.Length);
            
            foreach (var failure in failures)
            {
                grouper.Group(failure);
            }
            session.SaveChanges();

            Logger.InfoFormat("Done batching {0} messages failures.", failures.Length);


            if (totaltNumberOfFailuresAvailable > failures.Length)
            {
                Logger.InfoFormat("Still {0} messages failures that need grouping. Kicking of new batch.", totaltNumberOfFailuresAvailable - failures.Length);
                bus.SendLocal(new GroupOldFailures());
            }
        }

        MessageFailureHistory[] GetFailuresMissingOneOrMoreGroupers(int numberOfGroupers, out int totaltNumberOfFailuresAvailable)
        {
            RavenQueryStatistics withoufFailureGroupsStats;
            var withoutFailureGroupsFailures = GetFailuresWithoutFailureGroups(BatchSize, out withoufFailureGroupsStats);
            var leftInBatch = BatchSize - withoutFailureGroupsFailures.Length;

            RavenQueryStatistics notGroupedByAllGroupersStats;
            var notGroupedByAllGroupersFailures = GetFailuresNotGroupedByAllGroupers(numberOfGroupers, leftInBatch, out notGroupedByAllGroupersStats);

            var failures = withoutFailureGroupsFailures.Concat(notGroupedByAllGroupersFailures).ToArray();
            totaltNumberOfFailuresAvailable = withoufFailureGroupsStats.TotalResults + notGroupedByAllGroupersStats.TotalResults;
            return failures;
        }

        MessageFailureHistory[] GetFailuresWithoutFailureGroups(int batchSize, out RavenQueryStatistics stats)
        {
            return session.Query<MessageFailureHistory, MessageFailuresWithoutFailureGroupsIndex>()
                .Statistics(out stats)
                .Take(batchSize)
                .ToArray();
        }

        MessageFailureHistory[] GetFailuresNotGroupedByAllGroupers(int numberOfGroupers, int batchSize, out RavenQueryStatistics stats)
        {
            return session.Query<MessageFailureHistory, MessageFailuresByFailureGroupsIndex>()
                .Statistics(out stats)
                .Where(f => f.FailureGroups.Count < numberOfGroupers)
                .Take(batchSize)
                .ToArray();
        }
    }
}
