namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Threading;
    using CompositeViews.Messages;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class ScatterGatherVersionTests
    {
        [Test]
        public void A_composite_is_absent_when_an_instance_reports_no_version()
        {
            var api = new LocalAndRemoteApi();

            var everyone = api.AggregateResults(Context(), [Page("local", "a"), Page("remote", "b")]);
            var oneSilent = api.AggregateResults(Context(), [Page("local", "a"), Page("remote", null)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(everyone.QueryStats.Version.HasValue, Is.True, "every instance answered, so the composite covers them all");
                Assert.That(oneSilent.QueryStats.Version.HasValue, Is.False,
                    "a composite that ignored an instance would stop reporting that instance's changes");
            }
        }

        [Test]
        public void A_composite_moves_when_one_instance_moves()
        {
            var api = new LocalAndRemoteApi();

            var before = api.AggregateResults(Context(), [Page("local", "a"), Page("remote", "b")]);
            var after = api.AggregateResults(Context(), [Page("local", "a"), Page("remote", "c")]);

            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "one instance reported new data, so the composite cannot stay put");
        }

        [Test]
        public void A_remote_only_api_reports_the_version_of_the_instances_that_have_the_data()
        {
            var settings = new Settings();
            var api = new RemoteOnlyApi(settings);

            var composite = api.AggregateResults(Context(), [NoLocalData(settings.InstanceId), Page("remote", "b")]);

            Assert.That(composite.QueryStats.Version.HasValue, Is.True,
                "the remote answered with a version, so discarding it would leave the response with no ETag at all");
        }

        [Test]
        public void A_remote_only_api_reports_no_version_when_no_remote_answered()
        {
            var settings = new Settings();
            var api = new RemoteOnlyApi(settings);

            var composite = api.AggregateResults(Context(), [NoLocalData(settings.InstanceId)]);

            Assert.That(composite.QueryStats.Version.HasValue, Is.False,
                "nothing reported a version, so there is nothing to promise a caller");
        }

        static ScatterGatherApiMessageViewContext Context() => new(new PagingInfo(), new SortInfo());

        static QueryResult<IList<MessagesView>> Page(string instanceId, string validator) =>
            new([new MessagesView { MessageId = instanceId }], QueryStatsInfo.Fresh(DataVersion.FromToken(validator), 1))
            {
                InstanceId = instanceId
            };

        // What ScatterGatherRemoteOnly.LocalQuery returns: no rows and no version.
        static QueryResult<IList<MessagesView>> NoLocalData(string instanceId) =>
            new(null, QueryStatsInfo.Zero) { InstanceId = instanceId };

        class LocalAndRemoteApi() : ScatterGatherApiMessageView<object, ScatterGatherApiMessageViewContext>(
            null, null, null, null, NullLogger<LocalAndRemoteApi>.Instance)
        {
            protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewContext input, CancellationToken cancellationToken = default) =>
                throw new System.NotImplementedException();
        }

        class RemoteOnlyApi(Settings settings) : ScatterGatherRemoteOnly<ScatterGatherApiMessageViewContext, IList<MessagesView>>(
            settings, null, null, NullLogger<RemoteOnlyApi>.Instance)
        {
            protected override IList<MessagesView> ProcessResults(ScatterGatherApiMessageViewContext input, QueryResult<IList<MessagesView>>[] results) =>
                [.. results.Where(result => result.Results is not null).SelectMany(result => result.Results)];
        }
    }
}
