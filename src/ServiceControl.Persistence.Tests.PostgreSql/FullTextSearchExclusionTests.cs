namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFCore.PostgreSql;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Persistence.Infrastructure;

class FullTextSearchExclusionTests : ErrorIngestionTestBase
{
    static readonly string[] ExcludedKeys =
    [
        "NServiceBus.MessageId", "NServiceBus.ConversationId", "NServiceBus.CorrelationId",
        "NServiceBus.RelatedTo", "NServiceBus.TimeSent", "NServiceBus.ProcessingStarted",
        "NServiceBus.ProcessingEnded", "NServiceBus.DeliverAt", "NServiceBus.Timeout.Expire",
        "NServiceBus.Retries.Timestamp", "NServiceBus.ExceptionInfo.TimeOfFailure",
        "NServiceBus.TimeOfFailure", "NServiceBus.NonDurableMessage", "NServiceBus.TimeToBeReceived"
    ];

    static Dictionary<string, string> ExcludedHeaderValues() =>
        new(ExcludedKeys.Select((key, i) =>
            new KeyValuePair<string, string>(key, $"excludedmarker{i:D2}")));

    [SetUp]
    public void AllowLargeInlineBodies() => EFSettings.BodyStorage.MaxBodySizeToStore = 512 * 1024;

    [Test]
    public async Task Excluded_header_values_are_not_searchable()
    {
        var excluded = ExcludedHeaderValues();
        var matching = new IngestedFailure();
        matching.Headers["Order.Reference"] = "retainedpositivecontrol";
        foreach (var header in excluded)
        {
            matching.Headers[header.Key] = header.Value;
        }

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing" });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("retainedpositivecontrol", new PagingInfo(), new SortInfo()),
            matching);

        foreach (var value in excluded.Values)
        {
            var result = await MessagesViewStore.GetAllMessagesForSearch(value, new PagingInfo(), new SortInfo());
            Assert.That(result.Results, Is.Empty,
                $"Excluded header value '{value}' should not be searchable");
        }
    }

    [Test]
    public async Task Retained_custom_header_and_traceparent_are_searchable()
    {
        var matching = new IngestedFailure
        {
            ExceptionMessage = "retainedmarkerstandard"
        };
        matching.Headers["Order.Reference"] = "retainedmarkercustom";
        matching.Headers["traceparent"] = "retainedmarkertrace";

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing" });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("retainedmarkercustom", new PagingInfo(), new SortInfo()),
            matching);

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("retainedmarkertrace", new PagingInfo(), new SortInfo()),
            matching);

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("retainedmarkerstandard", new PagingInfo(), new SortInfo()),
            matching);
    }

    [Test]
    public async Task Body_value_is_searchable_even_when_also_in_excluded_header()
    {
        const string marker = "bodymarkeralsoinheader";

        var matching = new IngestedFailure
        {
            Body = Encoding.UTF8.GetBytes($"<order>{marker}</order>")
        };
        matching.Headers["NServiceBus.CorrelationId"] = marker;

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing" });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch(marker, new PagingInfo(), new SortInfo()),
            matching);
    }

    [Test]
    public async Task Excluded_header_values_are_not_searchable_with_empty_body()
    {
        var excluded = ExcludedHeaderValues();
        var matching = new IngestedFailure
        {
            Body = []
        };
        matching.Headers["Order.Reference"] = "retainedpositivecontrol";
        foreach (var header in excluded)
        {
            matching.Headers[header.Key] = header.Value;
        }

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing", Body = [] });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("retainedpositivecontrol", new PagingInfo(), new SortInfo()),
            matching);

        foreach (var value in excluded.Values)
        {
            var result = await MessagesViewStore.GetAllMessagesForSearch(value, new PagingInfo(), new SortInfo());
            Assert.That(result.Results, Is.Empty,
                $"Excluded header value '{value}' should not be searchable with an empty body");
        }
    }

    [Test]
    public async Task Retained_header_key_name_is_not_searchable()
    {
        var matching = new IngestedFailure();
        matching.Headers["traceparent"] = "traceparentvaluecontrol";

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing" });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("traceparentvaluecontrol", new PagingInfo(), new SortInfo()),
            matching);

        var keyResult = await MessagesViewStore.GetAllMessagesForSearch("traceparent", new PagingInfo(), new SortInfo());
        Assert.That(keyResult.Results, Is.Empty,
            "A header key name should not be searchable because jsonb_to_tsvector with [\"string\"] does not index keys");
    }

    [Test]
    public async Task Body_content_beyond_indexed_cap_is_not_searchable()
    {
        var withinCapMarker = "withincapmarkerxyz";
        var beyondCapMarker = "beyondcapmarkerxyz";
        var padding = new string('x', FullTextSearchSql.IndexedBodyLength);
        var bodyText = $"{withinCapMarker} {padding} {beyondCapMarker}";

        var matching = new IngestedFailure
        {
            ContentType = "text/plain",
            Body = Encoding.UTF8.GetBytes(bodyText)
        };

        await Ingest(matching, new IngestedFailure { EndpointName = "Billing" });

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch(withinCapMarker, new PagingInfo(), new SortInfo()),
            matching);

        var beyondResult = await MessagesViewStore.GetAllMessagesForSearch(beyondCapMarker, new PagingInfo(), new SortInfo());
        Assert.That(beyondResult.Results, Is.Empty,
            "Body content beyond the indexed body length cap should not be searchable");
    }

    [Test]
    public async Task Negative_search_for_nonexistent_term_returns_nothing()
    {
        var first = new IngestedFailure { ExceptionMessage = "the zarquon overheated" };
        var second = new IngestedFailure { ExceptionMessage = "the flux capacitor melted" };

        await Ingest(first, second);

        await WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch("zarquon", new PagingInfo(), new SortInfo()),
            first);

        var result = await MessagesViewStore.GetAllMessagesForSearch("qqqqwwwwzzzzerprprpnotreal", new PagingInfo(), new SortInfo());
        Assert.That(result.Results, Is.Empty);
    }

    static async Task WaitForSearchResults(Func<Task<QueryResult<IList<MessagesView>>>> search, params IngestedFailure[] expected)
    {
        var expectedIds = expected.Select(failure => failure.UniqueMessageIdString).OrderBy(id => id).ToArray();
        IList<MessagesView> results = [];

        await WaitUntil(async () =>
        {
            results = (await search()).Results;
            return results.Count == expectedIds.Length;
        }, $"Search returned {expectedIds.Length} message(s)", TimeSpan.FromSeconds(30));

        Assert.That(results.Select(view => view.Id).OrderBy(id => id), Is.EqualTo(expectedIds));
    }
}