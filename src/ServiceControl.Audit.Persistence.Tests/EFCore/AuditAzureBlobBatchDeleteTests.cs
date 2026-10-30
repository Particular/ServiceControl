namespace ServiceControl.Audit.Persistence.Tests.EFCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using NUnit.Framework;
using ServiceControl.Audit.Persistence.EFCore.Implementation.BodyStorage;

[TestFixture]
class AuditAzureBlobBatchDeleteTests
{
    static readonly DateTime Hour = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

    const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=fake;AccountKey=ZmFrZQ==;EndpointSuffix=core.windows.net";

    [Test]
    public async Task Deletes_in_batches_of_at_most_256_across_listing_pages()
    {
        var service = new FakeBlobService(Enumerable.Range(0, 600).Select(i => $"2026102010/{i}").Concat(["2026102009/a", "2026102011/a"])) { PageSize = 250 };

        await CreatePersistence(service).DeleteBodiesForHour(Hour);

        Assert.Multiple(() =>
        {
            Assert.That(service.BatchSizes, Is.EqualTo(new[] { 256, 256, 88 }));
            Assert.That(service.Blobs, Is.EquivalentTo(new[] { "2026102009/a", "2026102011/a" }));
        });
    }

    [Test]
    public async Task Deletes_only_blobs_of_the_requested_hour()
    {
        var service = new FakeBlobService(["2026102010/a", "2026102011/a"]);

        await CreatePersistence(service).DeleteBodiesForHour(Hour);

        Assert.That(service.Blobs, Is.EqualTo(new[] { "2026102011/a" }));
    }

    [Test]
    public async Task Sends_no_batch_when_nothing_matches()
    {
        var service = new FakeBlobService([]);

        await CreatePersistence(service).DeleteBodiesForHour(Hour);

        Assert.That(service.BatchSizes, Is.Empty);
    }

    [Test]
    public void Treats_a_blob_that_is_already_gone_as_deleted()
    {
        var service = new FakeBlobService(["2026102010/a", "2026102010/b"]) { StatusFor = name => name.EndsWith("/a") ? 404 : null };

        Assert.DoesNotThrowAsync(() => CreatePersistence(service).DeleteBodiesForHour(Hour));
    }

    [Test]
    public async Task Fails_when_one_delete_in_a_successful_batch_fails_and_a_retry_completes()
    {
        var service = new FakeBlobService(["2026102010/a", "2026102010/b"]) { StatusFor = name => name.EndsWith("/b") ? 403 : null };
        var store = CreatePersistence(service);

        var ex = Assert.ThrowsAsync<RequestFailedException>(() => store.DeleteBodiesForHour(Hour));

        Assert.That(ex.Status, Is.EqualTo(403));
        Assert.That(service.Blobs, Is.EqualTo(new[] { "2026102010/b" }));

        service.StatusFor = _ => null;
        await store.DeleteBodiesForHour(Hour);

        Assert.That(service.Blobs, Is.Empty);
    }

    [Test]
    public void Stops_after_a_failing_batch()
    {
        var service = new FakeBlobService(Enumerable.Range(0, 300).Select(i => $"2026102010/{i}")) { StatusFor = _ => 500 };

        Assert.ThrowsAsync<RequestFailedException>(() => CreatePersistence(service).DeleteBodiesForHour(Hour));
        Assert.That(service.BatchSizes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Deletes_every_hour_of_the_day_and_no_other_day()
    {
        var service = new FakeBlobService(["2026102000/a", "2026102023/a", "2026101923/a", "2026102100/a"]);

        await CreatePersistence(service).DeleteBodiesForDay(Hour);

        Assert.That(service.Blobs, Is.EquivalentTo(new[] { "2026101923/a", "2026102100/a" }));
    }

    [Test]
    public void Honors_cancellation_before_starting()
    {
        var service = new FakeBlobService(["2026102010/a"]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => CreatePersistence(service).DeleteBodiesForHour(Hour, cts.Token));
        Assert.That(service.Blobs, Has.Count.EqualTo(1));
    }

    [Test]
    public void Honors_cancellation_between_batches()
    {
        using var cts = new CancellationTokenSource();
        var service = new FakeBlobService(Enumerable.Range(0, 300).Select(i => $"2026102010/{i}")) { AfterBatch = cts.Cancel };

        Assert.CatchAsync<OperationCanceledException>(() => CreatePersistence(service).DeleteBodiesForHour(Hour, cts.Token));
        Assert.That(service.BatchSizes, Has.Count.EqualTo(1), "no batch is sent after cancellation");
        Assert.That(service.Blobs, Has.Count.EqualTo(44));
    }

    static AuditAzureBlobBodyStorage CreatePersistence(FakeBlobService service)
    {
        var options = new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(service)),
            Retry = { MaxRetries = 0 }
        };
        return new AuditAzureBlobBodyStorage(new BlobContainerClient(ConnectionString, "bodies", options), 64);
    }

    sealed class FakeBlobService(IEnumerable<string> initialBlobs) : HttpMessageHandler
    {
        static readonly Regex ContentIdPattern = new(@"Content-ID: (\d+)");
        static readonly Regex DeleteTargetPattern = new(@"DELETE /bodies/(\S+?) HTTP/1.1");

        public List<string> Blobs { get; } = initialBlobs.ToList();
        public List<int> BatchSizes { get; } = [];
        public int Requests { get; private set; }
        public int PageSize { get; set; } = 1000;
        public Action AfterBatch { get; set; } = () => { };

        // A status for the blob, or null to delete it, or report it missing when it is already gone.
        public Func<string, int?> StatusFor { get; set; } = _ => null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            Requests++;
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);

            if (query["comp"] == "batch")
            {
                var response = BatchResponse(await request.Content!.ReadAsStringAsync(cancellationToken));
                AfterBatch();
                return response;
            }

            // Stable name markers keep deletions from changing which test blobs appear on the next page.
            var marker = query["marker"] ?? "";
            var matching = Blobs.Where(name => name.StartsWith(query["prefix"] ?? "", StringComparison.Ordinal) && string.CompareOrdinal(name, marker) > 0)
                .OrderBy(name => name, StringComparer.Ordinal).ToList();
            var page = matching.Take(PageSize).ToList();
            var next = matching.Count > page.Count ? page[^1] : "";

            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><EnumerationResults ContainerName=\"bodies\"><Blobs>");
            foreach (var name in page)
            {
                xml.Append($"<Blob><Name>{name}</Name><Properties><Content-Length>1</Content-Length></Properties></Blob>");
            }
            xml.Append($"</Blobs><NextMarker>{next}</NextMarker></EnumerationResults>");

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml") };
        }

        HttpResponseMessage BatchResponse(string body)
        {
            var ids = ContentIdPattern.Matches(body).Select(m => m.Groups[1].Value).ToArray();
            var names = DeleteTargetPattern.Matches(body).Select(m => m.Groups[1].Value).ToArray();
            BatchSizes.Add(names.Length);

            var response = new StringBuilder();
            for (var i = 0; i < names.Length; i++)
            {
                var status = StatusFor(names[i]) ?? (Blobs.Remove(names[i]) ? 202 : 404);
                response.Append($"--batchresponse_1\r\nContent-Type: application/http\r\nContent-ID: {ids[i]}\r\n\r\nHTTP/1.1 {status} X\r\nx-ms-request-id: r{i}\r\nx-ms-error-code: Fake\r\n\r\n");
            }
            response.Append("--batchresponse_1--\r\n");

            var content = new StringContent(response.ToString(), Encoding.UTF8);
            content.Headers.ContentType = new("multipart/mixed") { Parameters = { new("boundary", "batchresponse_1") } };
            return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = content };
        }
    }
}
