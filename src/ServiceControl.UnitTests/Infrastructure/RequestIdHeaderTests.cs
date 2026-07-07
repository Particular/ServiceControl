namespace ServiceControl.UnitTests.Infrastructure;

using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using ServiceControl.Hosting.RequestId;

[TestFixture]
public class RequestIdHeaderTests
{
    [Test]
    public void Sets_the_request_trace_identifier()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "local-trace" };

        RequestIdHeader.Apply(context);

        Assert.That(context.Response.Headers[RequestIdHeader.HeaderName].ToString(), Is.EqualTo("local-trace"));
    }

    [Test]
    public void Keeps_a_request_id_proxied_from_a_remote_instance()
    {
        // A request forwarded to a remote instance (instance_id routing) is audited on the remote
        // under the remote's TraceIdentifier, which YARP copies back onto this response. That id is
        // the one the caller must see — overwriting it with the local proxy's TraceIdentifier would
        // hand out an operation id no audit entry matches.
        var context = new DefaultHttpContext { TraceIdentifier = "local-trace" };
        context.Response.Headers[RequestIdHeader.HeaderName] = "remote-trace";

        RequestIdHeader.Apply(context);

        Assert.That(context.Response.Headers[RequestIdHeader.HeaderName].ToString(), Is.EqualTo("remote-trace"));
    }
}
