#nullable enable

namespace ServiceControl.Audit.UnitTests.Mcp;

using System;
using System.Linq;
using System.Reflection;
using Audit.Mcp;
using NUnit.Framework;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

[TestFixture]
class McpMetadataDescriptionsTests
{
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessages), "read-only")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.SearchAuditMessages), "read-only")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByEndpoint), "read-only")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation), "read-only")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody), "read-only")]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetKnownEndpoints), "read-only")]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetEndpointAuditCounts), "read-only")]
    public void Audit_query_tools_are_described_as_read_only(Type toolType, string methodName, string expectedPhrase)
    {
        var description = GetMethodDescription(toolType, methodName);

        Assert.That(description, Does.Contain(expectedPhrase));
    }

    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody), "messageId", "audit message ID")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation), "conversationId", "conversation ID")]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetEndpointAuditCounts), "endpointName", "NServiceBus endpoint name")]
    public void Key_audit_tool_parameters_identify_the_entity_type(Type toolType, string methodName, string parameterName, string expectedPhrase)
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
