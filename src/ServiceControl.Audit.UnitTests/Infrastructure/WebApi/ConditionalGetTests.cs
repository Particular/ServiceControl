namespace ServiceControl.Audit.UnitTests.Infrastructure.WebApi
{
    using System.Net;
    using Audit.Infrastructure.WebApi;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using NUnit.Framework;

    [TestFixture]
    public class ConditionalGetTests
    {
        [Test]
        public void Repeating_a_request_with_the_etag_just_issued_is_not_modified()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag("4611686018427387904");
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

            httpContext.Response.WithEtag("4611686018427387904");
            httpContext.Request.Headers.IfNoneMatch = "\"something-else\"";

            var context = ResultExecuting(httpContext);

            new NotModifiedStatusHttpHandler().OnResultExecuting(context);

            Assert.That(context.Result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public void The_emitted_etag_is_a_well_formed_entity_tag()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag("4611686018427387904");

            Assert.That(httpContext.Response.GetTypedHeaders().ETag, Is.Not.Null,
                "an ETag that cannot be parsed as an entity-tag disables conditional GET without any error");
        }

        [Test]
        public void A_deterministic_etag_is_a_well_formed_entity_tag()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithDeterministicEtag("any-non-empty-payload-signature");

            Assert.That(httpContext.Response.GetTypedHeaders().ETag, Is.Not.Null);
        }

        [Test]
        public void The_emitted_etag_quotes_the_value_without_altering_it()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag("4611686018427387904");

            Assert.That(httpContext.Response.Headers.ETag.ToString(), Is.EqualTo("\"4611686018427387904\""));
        }

        [TestCase(null)]
        [TestCase("")]
        public void A_call_site_with_nothing_to_validate_emits_no_etag_header(string value)
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Response.WithEtag(value);

            Assert.That(httpContext.Response.Headers.ContainsKey("ETag"), Is.False,
                "an empty entity-tag is well formed, so it would match itself and answer 304 for unrelated payloads");
        }

        static ResultExecutingContext ResultExecuting(HttpContext httpContext) =>
            new(
                new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
                [],
                new OkObjectResult(new object()),
                controller: null);
    }
}
