#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
class RouteTemplateNormalizerTests
{
    [TestCase("api/errors/{failedMessageId:required:minlength(1)}/retry", "/api/errors/{failedMessageId}/retry")]
    [TestCase("api/configuration", "/api/configuration")]
    [TestCase("api/customchecks/{id}", "/api/customchecks/{id}")]
    [TestCase("api/errors/groups/{classifier?}", "/api/errors/groups/{classifier}")]
    [TestCase("api/messages/{*catchAll}", "/api/messages/{catchAll}")]
    [TestCase("api/my/routes", "/api/my/routes")]
    [TestCase("/api/already/rooted", "/api/already/rooted")]
    public void Strips_constraints_and_roots_the_template(string raw, string expected)
    {
        Assert.That(RouteTemplateNormalizer.Normalize(raw), Is.EqualTo(expected));
    }
}
