namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Audit.Auditing.MessagesView;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

class FullTextSearchExclusionTests : EFPersistenceTestFixture
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

    public override Task Setup()
    {
        SetSettings = settings => settings.MaxBodySizeToStore = 512 * 1024;
        return base.Setup();
    }

    [Test]
    public async Task Excluded_header_values_are_not_searchable()
    {
        var excluded = ExcludedHeaderValues();
        var headers = new Dictionary<string, string>(excluded)
        {
            ["Order.Reference"] = "retainedpositivecontrol"
        };

        await Ingest(MakeMessage(extraHeaders: headers));

        var positive = await MessagesViewStore.QueryMessages("retainedpositivecontrol", new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(positive.Results, Has.Count.EqualTo(1),
            "A retained custom header on the same message should be searchable before asserting negatives");

        foreach (var value in excluded.Values)
        {
            var found = await MessagesViewStore.QueryMessages(value, new PagingInfo(), new SortInfo("time_sent", "desc"));
            Assert.That(found.Results, Is.Empty,
                $"Excluded header value '{value}' should not be searchable");
        }
    }

    [Test]
    public async Task Retained_custom_header_and_traceparent_are_searchable()
    {
        var headers = new Dictionary<string, string>
        {
            ["Order.Reference"] = "retainedmarkercustom",
            ["traceparent"] = "retainedmarkertrace"
        };

        await Ingest(MakeMessage(extraHeaders: headers));

        using (Assert.EnterMultipleScope())
        {
            var custom = await MessagesViewStore.QueryMessages("retainedmarkercustom", new PagingInfo(), new SortInfo("time_sent", "desc"));
            Assert.That(custom.Results, Has.Count.EqualTo(1),
                "A retained custom header value should be searchable");

            var trace = await MessagesViewStore.QueryMessages("retainedmarkertrace", new PagingInfo(), new SortInfo("time_sent", "desc"));
            Assert.That(trace.Results, Has.Count.EqualTo(1),
                "A traceparent header value should be searchable");
        }
    }

    [Test]
    public async Task Body_value_is_searchable_even_when_also_in_excluded_header()
    {
        const string marker = "bodymarkeralsoinheader";

        var headers = new Dictionary<string, string>
        {
            ["NServiceBus.CorrelationId"] = marker
        };

        var message = MakeMessage(extraHeaders: headers);
        await Ingest(message, Encoding.UTF8.GetBytes($"<order>{marker}</order>"));

        var found = await MessagesViewStore.QueryMessages(marker, new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(found.Results, Has.Count.EqualTo(1),
            "A body value should be searchable even when the same value appears in an excluded header");
    }

    [Test]
    public async Task Excluded_header_values_are_not_searchable_with_empty_body()
    {
        var excluded = ExcludedHeaderValues();
        var headers = new Dictionary<string, string>(excluded)
        {
            ["Order.Reference"] = "retainedpositivecontrol"
        };

        var message = MakeMessage(contentType: null, extraHeaders: headers);
        await Ingest(message, []);

        var positive = await MessagesViewStore.QueryMessages("retainedpositivecontrol", new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(positive.Results, Has.Count.EqualTo(1),
            "A retained custom header on the same message should be searchable before asserting negatives");

        foreach (var value in excluded.Values)
        {
            var found = await MessagesViewStore.QueryMessages(value, new PagingInfo(), new SortInfo("time_sent", "desc"));
            Assert.That(found.Results, Is.Empty,
                $"Excluded header value '{value}' should not be searchable with an empty body");
        }
    }

    [Test]
    public async Task Retained_header_key_name_is_not_searchable()
    {
        var headers = new Dictionary<string, string>
        {
            ["traceparent"] = "traceparentvaluecontrol"
        };

        await Ingest(MakeMessage(extraHeaders: headers));

        var positive = await MessagesViewStore.QueryMessages("traceparentvaluecontrol", new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(positive.Results, Has.Count.EqualTo(1),
            "The traceparent header value should be searchable");

        var keyResult = await MessagesViewStore.QueryMessages("traceparent", new PagingInfo(), new SortInfo("time_sent", "desc"));
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

        await Ingest(MakeMessage(contentType: "text/plain"), Encoding.UTF8.GetBytes(bodyText));

        var withinResult = await MessagesViewStore.QueryMessages(withinCapMarker, new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(withinResult.Results, Has.Count.EqualTo(1),
            "Body content within the indexed body length cap should be searchable");

        var beyondResult = await MessagesViewStore.QueryMessages(beyondCapMarker, new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(beyondResult.Results, Is.Empty,
            "Body content beyond the indexed body length cap should not be searchable");
    }

    [Test]
    public async Task Negative_search_for_nonexistent_term_returns_nothing()
    {
        await Ingest(MakeMessage());

        var found = await MessagesViewStore.QueryMessages("qqqqwwwwzzzzerprprpnotreal", new PagingInfo(), new SortInfo("time_sent", "desc"));
        Assert.That(found.Results, Is.Empty);
    }
}