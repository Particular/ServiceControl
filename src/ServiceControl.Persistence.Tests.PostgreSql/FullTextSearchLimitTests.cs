namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

class FullTextSearchLimitTests : ErrorIngestionTestBase
{
    const int TokenCount = 150_000;

    [SetUp]
    public void StoreLargeBodiesInline() => EFSettings.BodyStorage.MaxBodySizeToStore = 2 * 1024 * 1024;

    [Test]
    public async Task Ingests_and_searches_a_body_with_more_distinct_words_than_a_tsvector_can_hold()
    {
        var body = string.Join(' ', Enumerable.Range(0, TokenCount).Select(token => $"w{token:D7}"));
        var failure = new IngestedFailure { ContentType = "text/plain", Body = Encoding.UTF8.GetBytes(body) };

        Assert.That(failure.Body, Has.Length.GreaterThan(1024 * 1024), "The body has to be larger than the 1 MB a tsvector can hold");

        await Ingest(failure);

        var result = await MessagesViewStore.GetAllMessagesForSearch("w0000001", new PagingInfo(), new SortInfo());

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
    }
}
