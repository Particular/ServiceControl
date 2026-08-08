namespace ServiceControl.UnitTests.Infrastructure.WebApi;

using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Persistence.Infrastructure;

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

        // RFC 9110 requires an entity-tag to be a quoted string. GetTypedHeaders parses through
        // EntityTagHeaderValue and yields null for anything else.
        Assert.That(httpContext.Response.GetTypedHeaders().ETag, Is.Not.Null,
            "an ETag that cannot be parsed as an entity-tag disables conditional GET without any error");
    }

    [Test]
    public void The_emitted_etag_quotes_the_value_without_altering_it()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));

        Assert.That(httpContext.Response.Headers.ETag.ToString(), Is.EqualTo("\"4611686018427387904\""));
    }

    [Test]
    public void A_data_version_emits_the_same_header_the_string_overload_did()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Response.WithEtag(DataVersion.FromToken("A:2-abc"));

        // Marking comes later. This change only moves who holds the value.
        Assert.That(httpContext.Response.Headers.ETag.ToString(), Is.EqualTo("\"A:2-abc\""));
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
    public void A_paged_endpoint_emits_the_store_version_rather_than_a_hash_of_it()
    {
        var httpContext = new DefaultHttpContext();
        var version = DataVersion.FromToken("4611686018427387904");

        httpContext.Response.WithQueryStatsAndPagingInfo(
            new QueryStatsInfo(version, totalCount: 1, isStale: false),
            new PagingInfo());

        // A hashed validator matches nothing a store holds, so the endpoint can never skip its query.
        Assert.That(httpContext.Response.Headers.ETag.ToString(), Does.Contain(version.ToString()));
    }

    static ResultExecutingContext ResultExecuting(HttpContext httpContext) =>
        new(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            new OkObjectResult(new object()),
            controller: null);
}
