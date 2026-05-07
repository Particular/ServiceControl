namespace ServiceControl.AcceptanceTests.Mcp;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.AcceptanceTesting.Mcp;

[TestFixture]
class When_mcp_server_is_enabled : AcceptanceTest
{
    [SetUp]
    public void EnableMcp() => SetSettings = s => s.EnableMcpServerWriteMode = true;

    [Test]
    public async Task Should_expose_mcp_endpoint()
    {
        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var response = await InitializeMcpSession();
                return response.StatusCode == HttpStatusCode.OK;
            })
            .Run();
    }

    [Test]
    public async Task Should_list_primary_instance_tools()
    {
        string toolsJson = null;

        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var session = await InitializeAndGetSessionInfo();
                if (session == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(session, "tools/list", new { });
                if (response == null)
                {
                    return false;
                }

                toolsJson = await ReadMcpResponseJson(response);
                return response.StatusCode == HttpStatusCode.OK;
            })
            .Run();

        Assert.That(toolsJson, Is.Not.Null);
        var mcpResponse = McpAcceptanceTestSupport.DeserializeListToolsResponse(toolsJson);
        var sortedTools = mcpResponse.Result.Tools.Cast<JsonElement>().OrderBy(t => t.GetProperty("name").GetString()).ToList();
        AssertPrimaryTools(sortedTools);
        McpAcceptanceTestSupport.AssertToolsHaveOutputSchema(sortedTools);
        var formattedTools = McpAcceptanceTestSupport.FormatToolsForApproval(sortedTools);
        Approver.Verify(formattedTools);
    }

    [Test]
    public async Task Should_call_get_errors_summary_tool()
    {
        string toolResult = null;

        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var session = await InitializeAndGetSessionInfo();
                if (session == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(session, "tools/call", new
                {
                    name = "get_errors_summary",
                    arguments = new { }
                });

                if (response == null || response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                toolResult = await ReadMcpResponseJson(response);
                return true;
            })
            .Run();

        Assert.That(toolResult, Is.Not.Null);
        var mcpResponse = McpAcceptanceTestSupport.DeserializeCallToolResponse(toolResult);
        McpAcceptanceTestSupport.AssertStructuredToolResponse(toolResult, mcpResponse.Result.StructuredContent, mcpResponse.Result.Content, structuredContent =>
        {
            Assert.That(structuredContent.GetProperty("unresolved").GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(structuredContent.GetProperty("archived").GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(structuredContent.GetProperty("resolved").GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(structuredContent.GetProperty("retryIssued").GetInt32(), Is.GreaterThanOrEqualTo(0));
        });
    }

    static void AssertPrimaryTools(IReadOnlyCollection<JsonElement> tools)
    {
        Assert.That(tools, Has.Count.EqualTo(19));

        var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();

        Assert.That(names, Does.Contain("get_errors_summary"));
        Assert.That(names, Does.Contain("get_failed_messages"));
        Assert.That(names, Does.Contain("get_failure_groups"));
        Assert.That(names, Does.Contain("retry_failed_messages"));
        Assert.That(names, Does.Contain("archive_failed_messages"));
    }

    Task<HttpResponseMessage> InitializeMcpSession() => McpAcceptanceTestSupport.InitializeMcpSession(HttpClient);

    Task<McpSessionInfo> InitializeAndGetSessionInfo() => McpAcceptanceTestSupport.InitializeAndGetSessionInfo(HttpClient);

    Task<HttpResponseMessage> SendMcpRequest(McpSessionInfo sessionInfo, string method, object @params) => McpAcceptanceTestSupport.SendMcpRequest(HttpClient, sessionInfo, method, @params);

    static Task<string> ReadMcpResponseJson(HttpResponseMessage response) => McpAcceptanceTestSupport.ReadMcpResponseJson(response);
}
