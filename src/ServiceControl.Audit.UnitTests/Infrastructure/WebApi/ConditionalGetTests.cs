namespace ServiceControl.Audit.UnitTests.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Net;
    using Audit.Auditing.MessagesView;
    using Audit.Infrastructure;
    using Audit.Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    [TestFixture]
    public class ConditionalGetTests
    {
        [Test]
        public void Repeating_a_request_with_the_etag_just_issued_is_not_modified()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));
            httpContext.Request.Headers.IfNoneMatch = httpContext.Response.Headers.ETag;

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>(),
                "the client already holds this version, so the response must be a 304 rather than the full payload");
            Assert.That(((StatusCodeResult)context.Result).StatusCode, Is.EqualTo((int)HttpStatusCode.NotModified));
        }

        [Test]
        public void A_different_etag_still_returns_the_payload()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));
            httpContext.Request.Headers.IfNoneMatch = "\"something-else\"";

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public void The_emitted_etag_is_a_well_formed_entity_tag()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));

            Assert.That(httpContext.Response.GetTypedHeaders().ETag, Is.Not.Null,
                "an ETag that cannot be parsed as an entity-tag disables conditional GET without any error");
        }

        [Test]
        public void Every_emitted_etag_is_marked_weak()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(httpContext.Response.Headers.ETag.ToString(), Is.EqualTo("W/\"4611686018427387904\""));
                Assert.That(httpContext.Response.GetTypedHeaders().ETag.IsWeak, Is.True);
            }
        }

        [Test]
        public void An_absent_data_version_emits_no_header()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.None);

            Assert.That(httpContext.Response.Headers.ContainsKey("ETag"), Is.False,
                "an empty entity-tag is well formed, so it would match itself and answer 304 for unrelated payloads");
        }

        [Test]
        public void An_unmarked_validator_from_an_older_client_still_matches()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));
            httpContext.Request.Headers.IfNoneMatch = "\"4611686018427387904\"";

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>(),
                "weak comparison ignores the W/ prefix, which is what carries a client through the upgrade");
        }

        [Test]
        public void A_wildcard_precondition_is_not_modified_when_a_representation_exists()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));
            httpContext.Request.Headers.IfNoneMatch = "*";

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>(),
                "RFC 9110: * matches whenever a current representation exists");
        }

        [Test]
        public void A_wildcard_precondition_is_ignored_when_there_is_no_validator()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Request.Headers.IfNoneMatch = "*";

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<OkObjectResult>(),
                "an endpoint that publishes no validator has nothing for a client to have cached");
        }

        [Test]
        public void A_failed_result_cannot_be_replaced_by_not_modified()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.WithEtag(DataVersion.FromToken("version"));
            httpContext.Request.Headers.IfNoneMatch = "*";
            var context = ResultExecuting(httpContext);
            context.Result = new NotFoundResult();

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<NotFoundResult>());
        }

        [Test]
        public async Task A_file_stream_is_disposed_when_replaced()
        {
            var httpContext = new DefaultHttpContext();
            var responseFeature = new CompletingResponseFeature();
            httpContext.Features.Set<IHttpResponseFeature>(responseFeature);
            httpContext.Response.WithEtag(DataVersion.FromToken("version"));
            httpContext.Request.Headers.IfNoneMatch = "*";
            var stream = new MemoryStream();
            var context = ResultExecuting(httpContext);
            context.Result = new FileStreamResult(stream, "application/octet-stream");

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>());
            await responseFeature.Complete();
            Assert.That(stream.CanRead, Is.False);
        }

        [Test]
        public void A_paged_endpoint_emits_the_store_version_rather_than_a_hash_of_it()
        {
            var httpContext = new DefaultHttpContext();
            var version = DataVersion.FromToken("4611686018427387904");

            httpContext.Response.WithQueryStatsAndPagingInfo(
                new QueryStatsInfo(version, totalCount: 1),
                new PagingInfo());

            Assert.That(httpContext.Response.Headers.ETag.ToString(), Does.Contain(version.ToString()));
        }

        class CompletingResponseFeature : IHttpResponseFeature
        {
            Func<object, Task> onCompleted;
            object onCompletedState;
            public int StatusCode { get; set; } = 200;
            public string ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = new MemoryStream();
            public bool HasStarted => false;
#pragma warning disable PS0013 // ASP.NET Core interface signatures cannot accept a cancellation token
            public void OnStarting(Func<object, Task> callback, object state) { }
            public void OnCompleted(Func<object, Task> callback, object state) => (onCompleted, onCompletedState) = (callback, state);
#pragma warning restore PS0013
            public Task Complete() => onCompleted(onCompletedState);
        }

        static ResultExecutingContext ResultExecuting(HttpContext httpContext) =>
            new(
                new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
                [],
                new OkObjectResult(new object()),
                controller: null);
    }
}