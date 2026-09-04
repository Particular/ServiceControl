namespace ServiceControl.Infrastructure.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using ServiceControl.Hosting.QueryTimeout;

[TestFixture]
public class QueryTimeoutResponseTests
{
    [Test]
    public async Task A_query_timeout_becomes_a_504_problem_that_names_the_setting()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var pipeline = QueryTimeoutResponse.Wrap(_ => throw new TimeoutException("The query did not complete within 60 seconds. The 'ServiceControl/QueryTimeoutInSeconds' setting can be used to change it."));

        await pipeline(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status504GatewayTimeout));
        Assert.That(context.Response.ContentType, Does.StartWith("application/problem+json"));

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);
        Assert.That(problem.Status, Is.EqualTo(504));
        Assert.That(problem.Detail, Does.Contain("ServiceControl/QueryTimeoutInSeconds"));
    }

    [Test]
    public async Task A_response_that_completes_is_passed_through()
    {
        var context = new DefaultHttpContext();

        var pipeline = QueryTimeoutResponse.Wrap(httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await pipeline(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
    }

    [Test]
    public void Other_failures_are_not_its_business()
    {
        var pipeline = QueryTimeoutResponse.Wrap(_ => throw new InvalidOperationException("boom"));

        Assert.ThrowsAsync<InvalidOperationException>(() => pipeline(new DefaultHttpContext()));
    }
}
