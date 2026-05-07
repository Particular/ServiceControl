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
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessages))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageById))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageLastAttempt))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetErrorsSummary))]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessagesByEndpoint))]
    [TestCase(typeof(FailureGroupTools), nameof(FailureGroupTools.GetFailureGroups))]
    [TestCase(typeof(FailureGroupTools), nameof(FailureGroupTools.GetRetryHistory))]
    public void Read_only_primary_tools_end_with_read_only_sentence(Type toolType, string methodName)
    {
        var description = GetMethodDescription(toolType, methodName);

        Assert.That(description, Does.EndWith("Read-only."));
    }

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

    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessages))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessagesByQueue))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessages))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessagesByEndpoint))]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailureGroup))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailedMessages))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailureGroup))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailedMessages))]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailureGroup))]
    public void Bulk_mutating_tools_warn_that_they_may_affect_many_messages(Type toolType, string methodName)
    {
        var description = GetMethodDescription(toolType, methodName);

        Assert.That(description, Does.Contain("may affect many messages"));
    }

    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageById), "failedMessageId", "failed message ID")]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageLastAttempt), "failedMessageId", "failed message ID")]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessage), "failedMessageId", "failed message ID")]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryFailedMessages), "messageIds", "failed message IDs")]
    [TestCase(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessagesByEndpoint), "endpointName", "endpoint name")]
    [TestCase(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessagesByEndpoint), "endpointName", "endpoint name")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailureGroup), "groupId", "failure group ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailureGroup), "groupId", "failure group ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailedMessage), "failedMessageId", "failed message ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.ArchiveFailedMessages), "messageIds", "failed message IDs")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailedMessage), "failedMessageId", "failed message ID")]
    [TestCase(typeof(ArchiveTools), nameof(ArchiveTools.UnarchiveFailedMessages), "messageIds", "failed message IDs")]
    public void Key_error_tool_parameters_identify_the_entity_type(Type toolType, string methodName, string parameterName, string expectedPhrase)
    {
        var description = GetParameterDescription(toolType, methodName, parameterName);

        Assert.That(description, Does.Contain(expectedPhrase));
    }

    [Test]
    public void Get_failed_messages_guides_agents_toward_groups_first_and_details_second()
    {
        var description = GetMethodDescription(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessages));

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("Retrieve failed messages"));
            Assert.That(description, Does.Contain("root-cause analysis"));
            Assert.That(description, Does.Contain("GetFailureGroups"));
            Assert.That(description, Does.Contain("GetFailedMessageById"));
        });
    }

    [Test]
    public void Get_failure_groups_is_positioned_as_root_cause_starting_point()
    {
        var description = GetMethodDescription(typeof(FailureGroupTools), nameof(FailureGroupTools.GetFailureGroups));

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("Retrieve failure groups"));
            Assert.That(description, Does.Contain("first step"));
            Assert.That(description, Does.Contain("root cause"));
            Assert.That(description, Does.Contain("GetFailedMessages"));
        });
    }

    [Test]
    public void Failed_message_detail_tools_reference_the_expected_workflow()
    {
        var byId = GetMethodDescription(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageById));
        var lastAttempt = GetMethodDescription(typeof(FailedMessageTools), nameof(FailedMessageTools.GetFailedMessageLastAttempt));

        Assert.Multiple(() =>
        {
            Assert.That(byId, Does.Contain("failed message ID"));
            Assert.That(byId, Does.Contain("GetFailedMessages").Or.Contain("GetFailureGroups"));

            Assert.That(lastAttempt, Does.Contain("last processing attempt").Or.Contain("most recent failure"));
            Assert.That(lastAttempt, Does.Contain("GetFailedMessages").Or.Contain("GetFailedMessageById"));
        });
    }

    [Test]
    public void Retry_tools_describe_targeted_group_and_broad_scenarios()
    {
        var retryByIds = GetMethodDescription(typeof(RetryTools), nameof(RetryTools.RetryFailedMessages));
        var retryGroup = GetMethodDescription(typeof(RetryTools), nameof(RetryTools.RetryFailureGroup));
        var retryAll = GetMethodDescription(typeof(RetryTools), nameof(RetryTools.RetryAllFailedMessages));

        Assert.Multiple(() =>
        {
            Assert.That(retryByIds, Does.Contain("specific").And.Contain("RetryFailureGroup"));

            Assert.That(retryGroup, Does.Contain("root cause").And.Contain("RetryFailedMessages"));

            Assert.That(retryAll, Does.Contain("explicitly requests").And.Contain("narrower retry tools"));
            Assert.That(retryAll, Does.Contain("large number of messages"));
        });
    }

    static MethodInfo GetMethod(Type toolType, string methodName)
        => toolType.GetMethod(methodName)!;

    static string GetMethodDescription(Type toolType, string methodName)
        => GetMethod(toolType, methodName)
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;

    static string GetParameterDescription(Type toolType, string methodName, string parameterName)
        => GetMethod(toolType, methodName)
            .GetParameters()
            .Single(p => p.Name == parameterName)
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;
}
