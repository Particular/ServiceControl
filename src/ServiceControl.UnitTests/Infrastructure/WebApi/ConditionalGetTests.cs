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
            QueryStatsInfo.Fresh(version, totalCount: 1),
            new PagingInfo());

        // A hashed validator matches nothing a store holds, so the endpoint can never skip its query.
        Assert.That(httpContext.Response.Headers.ETag.ToString(), Does.Contain(version.ToString()));
    }

    [Test]
    public void An_aggregate_derived_etag_is_marked_weak()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.Headers.ETag.ToString(), Is.EqualTo("W/\"4611686018427387904\""));
            Assert.That(httpContext.Response.GetTypedHeaders().ETag.IsWeak, Is.True);
        });
    }


    [Test]
    public void A_weak_validator_matches_under_the_comparison_If_None_Match_requires()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Response.WithEtag(DataVersion.FromToken("4611686018427387904"));
        httpContext.Request.Headers.IfNoneMatch = httpContext.Response.Headers.ETag;

        var context = ResultExecuting(httpContext);

        new NotModifiedStatusHttpHandler().OnResultExecuting(context);

        Assert.That(context.Result, Is.InstanceOf<StatusCodeResult>(),
            "RFC 9110 requires If-None-Match to use the weak comparison function, so a weak tag must match a weak tag");
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
    public void A_caller_holding_one_validator_hands_it_to_the_store()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Headers.IfNoneMatch = "W/\"4611686018427387904\"";

        Assert.That(httpContext.Request.GetKnownVersion().Matches(DataVersion.FromToken("4611686018427387904")), Is.True,
            "the store skips its whole query on this, so it has to survive the round trip through the header");
    }

    [Test]
    public void A_version_survives_the_round_trip_out_as_a_header_and_back()
    {
        var issued = DataVersion.FromToken("4611686018427387904");

        var httpContext = new DefaultHttpContext();
        httpContext.Response.WithEtag(issued);
        httpContext.Request.Headers.IfNoneMatch = httpContext.Response.Headers.ETag;

        Assert.That(httpContext.Request.GetKnownVersion().Matches(issued), Is.True,
            "the store cannot skip work for a version it can no longer recognise coming back");
    }

    [Test]
    public void A_caller_holding_several_validators_hands_the_store_none()
    {
        var httpContext = new DefaultHttpContext();

        // RFC 9110 allows a list. Reading the raw header would hand the store the whole list as one
        // malformed validator, which matches nothing and silently costs it the short circuit.
        httpContext.Request.Headers.IfNoneMatch = "\"first\", \"second\"";

        Assert.That(httpContext.Request.GetKnownVersion().HasValue, Is.False,
            "a store can only skip work for a single known version");
    }

    [Test]
    public void A_wildcard_precondition_is_not_a_known_version()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Headers.IfNoneMatch = "*";

        Assert.That(httpContext.Request.GetKnownVersion().HasValue, Is.False,
            "the wildcard asks whether any representation exists, which is not a version a store can match");
    }

    [Test]
    public void A_caller_holding_nothing_hands_the_store_nothing()
    {
        Assert.That(new DefaultHttpContext().Request.GetKnownVersion().HasValue, Is.False);
    }

    static ResultExecutingContext ResultExecuting(HttpContext httpContext) =>
        new(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            new OkObjectResult(new object()),
            controller: null);
}
