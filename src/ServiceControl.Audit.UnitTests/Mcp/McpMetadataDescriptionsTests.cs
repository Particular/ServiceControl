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
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessages))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.SearchAuditMessages))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByEndpoint))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody))]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetKnownEndpoints))]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetEndpointAuditCounts))]
    public void Read_only_audit_tools_end_with_read_only_sentence(Type toolType, string methodName)
    {
        var description = GetMethodDescription(toolType, methodName);

        Assert.That(description, Does.EndWith("Read-only."));
    }

    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody), "messageId", "audit message ID")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation), "conversationId", "conversation ID")]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetEndpointAuditCounts), "endpointName", "NServiceBus endpoint name")]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByEndpoint), "endpointName", "endpoint name")]
    public void Key_audit_tool_parameters_identify_the_entity_type(Type toolType, string methodName, string parameterName, string expectedPhrase)
    {
        var description = GetParameterDescription(toolType, methodName, parameterName);

        Assert.That(description, Does.Contain(expectedPhrase));
    }

    [Test]
    public void Audit_tools_distinguish_browse_search_trace_and_payload_scenarios()
    {
        var browse = GetMethodDescription(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessages));
        var search = GetMethodDescription(typeof(AuditMessageTools), nameof(AuditMessageTools.SearchAuditMessages));
        var conversation = GetMethodDescription(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation));
        var endpoint = GetMethodDescription(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByEndpoint));
        var body = GetMethodDescription(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody));
        var knownEndpoints = GetMethodDescription(typeof(EndpointTools), nameof(EndpointTools.GetKnownEndpoints));

        Assert.Multiple(() =>
        {
            Assert.That(browse, Does.Contain("browse recent message activity").And.Contain("SearchAuditMessages"));

            Assert.That(search, Does.Contain("specific business identifier or text").And.Contain("GetAuditMessages"));

            Assert.That(conversation, Does.Contain("conversation").And.Contain("multiple endpoints"));

            Assert.That(endpoint, Does.Contain("single endpoint").And.Contain("GetAuditMessagesByConversation"));

            Assert.That(body, Does.Contain("message payload").And.Contain("search or browsing tools"));

            Assert.That(knownEndpoints, Does.Contain("starting point").And.Contain("available endpoints"));
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
