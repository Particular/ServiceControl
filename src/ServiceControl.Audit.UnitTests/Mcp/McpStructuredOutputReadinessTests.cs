#nullable enable

namespace ServiceControl.Audit.UnitTests.Mcp;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Audit.Mcp;
using ModelContextProtocol.Server;
using NUnit.Framework;

[TestFixture]
class McpStructuredOutputReadinessTests
{
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessages))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.SearchAuditMessages))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByEndpoint))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessagesByConversation))]
    [TestCase(typeof(AuditMessageTools), nameof(AuditMessageTools.GetAuditMessageBody))]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetKnownEndpoints))]
    [TestCase(typeof(EndpointTools), nameof(EndpointTools.GetEndpointAuditCounts))]
    public void Migrated_audit_mcp_tools_opt_into_structured_content_and_do_not_return_task_of_string(Type toolType, string methodName)
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
