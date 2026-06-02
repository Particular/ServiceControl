#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using NUnit.Framework;
using ServiceControl.Mcp;

[TestFixture]
class McpStructuredOutputReadinessTests
{
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessages))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageById))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageLastAttempt))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetErrorsSummary))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessagesByEndpoint))]
    [TestCase(typeof(FailureGroupTools), nameof(FailureGroupTools.GetFailureGroups))]
    [TestCase(typeof(FailureGroupTools), nameof(FailureGroupTools.GetRetryHistory))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessage))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessages))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessagesByQueue))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessages))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessagesByEndpoint))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailureGroup))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailedMessage))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailedMessages))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailureGroup))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailedMessage))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailedMessages))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailureGroup))]
    public void Migrated_primary_mcp_tools_opt_into_structured_content_and_do_not_return_task_of_string(Type toolType, string methodName)
    {
        var method = GetMethod(toolType, methodName);
        var attribute = method.GetCustomAttribute<McpServerToolAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(attribute, Is.Not.Null, $"Expected {toolType.Name}.{methodName} to have an {nameof(McpServerToolAttribute)}.");
            Assert.That(attribute!.UseStructuredContent, Is.True);
            Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(Task<string>)));
        });
    }

    static MethodInfo GetMethod(Type toolType, string methodName)
        => toolType.GetMethod(methodName)!;
}
