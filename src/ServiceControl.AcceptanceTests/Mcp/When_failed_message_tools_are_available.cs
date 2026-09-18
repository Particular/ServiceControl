#nullable enable
namespace ServiceControl.AcceptanceTests.Mcp;

using System.Net;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceControl.AcceptanceTesting.Mcp;

[TestFixture]
class When_failed_message_tools_are_available : AcceptanceTest
{
    [SetUp]
    public void EnableMcp() => SetSettings = s => s.EnableMcpServer = true;

    [Test]
    public async Task Should_return_structured_content_for_a_missing_failed_message()
    {
        string? toolResult = null;

        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var session = await McpAcceptanceTestSupport.InitializeAndGetSessionInfo(HttpClient);
                if (session == null)
                {
                    return false;
                }

                var response = await McpAcceptanceTestSupport.SendMcpRequest(HttpClient, session, "tools/call", new
                {
                    name = "get_failed_message_by_id",
                    arguments = new { failedMessageId = "missing-message-id" }
                });

                if (response == null || response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                toolResult = await McpAcceptanceTestSupport.ReadMcpResponseJson(response);
                return true;
            })
            .Run();

        Assert.That(toolResult, Is.Not.Null);
        var mcpResponse = McpAcceptanceTestSupport.DeserializeCallToolResponse(toolResult);
        McpAcceptanceTestSupport.AssertStructuredToolResponse(toolResult, mcpResponse.Result.StructuredContent, mcpResponse.Result.Content, structuredContent =>
        {
            Assert.That(structuredContent.GetProperty("error").GetString(), Does.Contain("not found"));
        });
    }
}