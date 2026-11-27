namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

// Runs the real S3 client against a local listener, so it needs neither Docker nor LocalStack.
[TestFixture]
class S3BodyStorageConditionalWriteTests
{
    FakeS3Endpoint endpoint;

    [SetUp]
    public void StartEndpoint() => endpoint = new FakeS3Endpoint();

    [TearDown]
    public void StopEndpoint() => endpoint.Dispose();

    S3BodyStoragePersistence Store() => new(new S3BodyStorageSettings
    {
        MinCompressionSize = 64,
        ServiceUrl = endpoint.Url,
        Region = "us-east-1",
        Credentials = new S3StaticCredentials { AccessKeyId = "test", SecretAccessKey = "test" },
        BucketName = "bodies"
    });

    [Test]
    public async Task The_upload_creates_the_object_only_if_none_exists_and_keeps_its_content_type_and_metadata()
    {
        await Store().WriteBody("body-1", new byte[] { 1, 2, 3 }, "text/plain");

        var put = endpoint.Requests.Single(request => request.Method == "PUT");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(put.Headers["If-None-Match"], Is.EqualTo("*"), "without the condition a body stored after the existence check is replaced");
            Assert.That(put.Headers["Content-Type"], Is.EqualTo("text/plain"));
            Assert.That(put.Headers["x-amz-meta-body-size"], Is.EqualTo("3"));
            Assert.That(put.Headers["x-amz-meta-is-compressed"], Is.EqualTo("False"));
        }
    }

    [Test]
    public async Task A_compressed_upload_is_conditional_too()
    {
        await Store().WriteBody("body-2", new byte[10_000], "application/octet-stream");

        var put = endpoint.Requests.Single(request => request.Method == "PUT");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(put.Headers["If-None-Match"], Is.EqualTo("*"));
            Assert.That(put.Headers["x-amz-meta-is-compressed"], Is.EqualTo("True"));
            Assert.That(put.Headers["x-amz-meta-body-size"], Is.EqualTo("10000"));
        }
    }

    [Test]
    public async Task An_existing_object_is_not_uploaded_over()
    {
        endpoint.ObjectExists = true;

        await Store().WriteBody("body-3", new byte[] { 1 }, "text/plain");

        Assert.That(endpoint.Requests.Select(request => request.Method), Is.EqualTo(new[] { "HEAD" }));
    }

    [Test]
    public void An_object_stored_after_the_existence_check_is_left_alone_without_an_error()
    {
        endpoint.PutStatus = HttpStatusCode.PreconditionFailed;

        Assert.DoesNotThrowAsync(() => Store().WriteBody("body-4", new byte[] { 1 }, "text/plain"));
        Assert.That(endpoint.Requests.Count(request => request.Method == "PUT"), Is.EqualTo(1));
    }

    [Test]
    public void Any_other_storage_failure_still_fails_the_write()
    {
        endpoint.PutStatus = HttpStatusCode.Forbidden;

        var exception = Assert.ThrowsAsync<AmazonS3Exception>(() => Store().WriteBody("body-5", new byte[] { 1 }, "text/plain"));

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public void A_cancelled_write_is_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => Store().WriteBody("body-6", new byte[] { 1 }, "text/plain", cancelled.Token));
    }

    sealed class FakeS3Endpoint : IDisposable
    {
        readonly HttpListener listener = new();
        readonly object gate = new();
        readonly List<RecordedRequest> requests = [];

        public FakeS3Endpoint()
        {
            // The listener needs a free port, which it cannot report before it starts, so probe for one.
            for (var attempt = 0; ; attempt++)
            {
                var port = Random.Shared.Next(20000, 60000);
                var candidate = new HttpListener();
                candidate.Prefixes.Add($"http://localhost:{port}/");

                try
                {
                    candidate.Start();
                    listener = candidate;
                    Url = $"http://localhost:{port}";
                    break;
                }
                catch (HttpListenerException) when (attempt < 20)
                {
                    candidate.Close();
                }
            }

            _ = Serve();
        }

        public string Url { get; }
        public bool ObjectExists { get; set; }
        public HttpStatusCode PutStatus { get; set; } = HttpStatusCode.OK;

        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (gate)
                {
                    return [.. requests];
                }
            }
        }

        async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                var headers = context.Request.Headers.AllKeys.ToDictionary(key => key, key => context.Request.Headers[key], StringComparer.OrdinalIgnoreCase);

                lock (gate)
                {
                    requests.Add(new RecordedRequest(context.Request.HttpMethod, headers));
                }

                // The body is not needed, but the client waits for the server to read it.
                await context.Request.InputStream.CopyToAsync(Stream.Null);

                var status = context.Request.HttpMethod switch
                {
                    "HEAD" => ObjectExists ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                    "PUT" => PutStatus,
                    _ => HttpStatusCode.NotImplemented
                };

                context.Response.StatusCode = (int)status;

                if (context.Request.HttpMethod == "PUT" && status != HttpStatusCode.OK)
                {
                    var code = status == HttpStatusCode.PreconditionFailed ? "PreconditionFailed" : "AccessDenied";
                    var error = System.Text.Encoding.UTF8.GetBytes($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>{code}</Code><Message>{code}</Message><RequestId>1</RequestId></Error>");
                    context.Response.ContentType = "application/xml";
                    await context.Response.OutputStream.WriteAsync(error);
                }

                context.Response.Close();
            }
        }

        public void Dispose() => listener.Close();
    }

    sealed record RecordedRequest(string Method, Dictionary<string, string> Headers);
}
