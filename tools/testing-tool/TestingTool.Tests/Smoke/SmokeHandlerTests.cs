using NUnit.Framework;
using TestingTool.Smoke;

namespace TestingTool.Tests.Smoke;

public class SmokeHandlerTests
{
    [Test]
    public void Failure_headers_group_by_the_check()
    {
        var message = new SmokeMessage { Marker = "m1", Fail = true, FailureType = "TestingTool.Smoke.Failure_run_check" };
        var thrown = Assert.Throws<SmokeFailureException>(() => SmokeMessageHandler.Process(new SmokeObservations(), message, null, "Endpoint"));
        var faultHeaders = new Dictionary<string, string>
        {
            ["NServiceBus.FailedQ"] = "Source@dbo",
            ["NServiceBus.ExceptionInfo.ExceptionType"] = "TestingTool.Smoke.SmokeFailureException",
            ["NServiceBus.ExceptionInfo.Message"] = thrown!.Message
        };

        SmokeHeaders.CustomizeFailure(faultHeaders);

        Assert.That(faultHeaders["NServiceBus.ExceptionInfo.ExceptionType"], Is.EqualTo("TestingTool.Smoke.Failure_run_check"));
        Assert.That(faultHeaders["NServiceBus.FailedQ"], Is.EqualTo("Source@dbo"));
    }

    [Test]
    public void Failure_headers_leave_other_messages_untouched()
    {
        var faultHeaders = new Dictionary<string, string>
        {
            ["NServiceBus.FailedQ"] = "TestingTool.Load",
            ["NServiceBus.ExceptionInfo.ExceptionType"] = "System.Exception",
            ["NServiceBus.ExceptionInfo.Message"] = "Something|else"
        };

        SmokeHeaders.CustomizeFailure(faultHeaders);

        Assert.That(faultHeaders, Has.Count.EqualTo(3));
        Assert.That(faultHeaders["NServiceBus.ExceptionInfo.ExceptionType"], Is.EqualTo("System.Exception"));
    }

    [Test]
    public void Redirect_check_passes_only_when_the_target_handled_the_retry()
    {
        Assert.DoesNotThrow(() => SmokeChecks.RequireHandledBy(new SmokeObservations.HandledMessage(null, "", "Target"), "Target", "Source"));
        var bySource = Assert.Throws<SmokeCheckFailedException>(() => SmokeChecks.RequireHandledBy(new SmokeObservations.HandledMessage(null, "", "Source"), "Target", "Source"));
        Assert.That(bySource!.Message, Does.Contain("handled by 'Source'"));
        Assert.Throws<SmokeCheckFailedException>(() => SmokeChecks.RequireHandledBy(null, "Target", "Source"));
    }

    [Test]
    public void A_failing_smoke_message_throws_until_its_marker_is_released()
    {
        var observations = new SmokeObservations();
        var message = new SmokeMessage { Marker = "m1", Fail = true };

        Assert.Throws<SmokeFailureException>(() => SmokeMessageHandler.Process(observations, message, null, "Endpoint"));
        Assert.That(observations.FindHandled("m1"), Is.Null);

        observations.Release("m1");
        SmokeMessageHandler.Process(observations, message, "failed-1", "Endpoint-A");

        Assert.That(observations.FindHandled("m1")!.EditOf, Is.EqualTo("failed-1"));
        Assert.That(observations.FindHandled("m1")!.Endpoint, Is.EqualTo("Endpoint-A"));
    }

    [Test]
    public void Releasing_one_marker_does_not_release_another()
    {
        var observations = new SmokeObservations();
        observations.Release("m1");

        Assert.Throws<SmokeFailureException>(() => SmokeMessageHandler.Process(observations, new SmokeMessage { Marker = "m2", Fail = true }, null, "Endpoint"));
    }
}
