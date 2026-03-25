#nullable enable

namespace ServiceControl.UnitTests.Mcp;

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ServiceControl.Mcp;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

[TestFixture]
class McpMetadataDescriptionsTests
{
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
    public void Mutating_tools_explicitly_warn_that_they_change_system_state(Type toolType, string methodName)
    {
        var description = GetMethodDescription(toolType, methodName);

        Assert.That(description, Does.Contain("changes system state"));
    }

    [Test]
    public void Retry_all_failed_messages_warns_that_it_affects_all_unresolved_failed_messages()
    {
        var description = GetMethodDescription(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessages));

        Assert.That(description, Does.Contain("all unresolved failed messages across the instance"));
    }

    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageById), "failedMessageId", "failed message ID")]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageLastAttempt), "failedMessageId", "failed message ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailureGroup), "groupId", "failure group ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailureGroup), "groupId", "failure group ID")]
    public void Key_error_tool_parameters_identify_the_entity_type(Type toolType, string methodName, string parameterName, string expectedPhrase)
    {
        var description = GetParameterDescription(toolType, methodName, parameterName);

        Assert.That(description, Does.Contain(expectedPhrase));
    }

    static string GetMethodDescription(Type toolType, string methodName)
        => toolType.GetMethod(methodName)!
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;

    static string GetParameterDescription(Type toolType, string methodName, string parameterName)
        => toolType.GetMethod(methodName)!
            .GetParameters()
            .Single(p => p.Name == parameterName)
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;
}
