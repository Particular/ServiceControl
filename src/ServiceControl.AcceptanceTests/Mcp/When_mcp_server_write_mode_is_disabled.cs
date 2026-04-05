namespace ServiceControl.AcceptanceTests.Mcp;

using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceControl.AcceptanceTesting.Mcp;

[TestFixture]
class When_mcp_server_write_mode_is_disabled : AcceptanceTest
{
    [SetUp]
    public void EnableReadOnlyMcp() => SetSettings = s => s.EnableMcpServer = true;

    [Test]
    public async Task Should_not_expose_write_tools()
    {
        string[] toolNames = null;

        await Define<ScenarioContext>()
            .Done(async _ =>
            {
                var session = await McpAcceptanceTestSupport.InitializeAndGetSessionInfo(HttpClient);
                if (session == null)
                {
                    return false;
                }

                var response = await McpAcceptanceTestSupport.SendMcpRequest(HttpClient, session, "tools/list", new { });
                if (response == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return false;
                }

                var json = await McpAcceptanceTestSupport.ReadMcpResponseJson(response);
                var mcpResponse = McpAcceptanceTestSupport.DeserializeListToolsResponse(json);
                toolNames = mcpResponse.Result.Tools.Cast<JsonElement>()
                    .Select(t => t.GetProperty("name").GetString())
                    .ToArray();
                return true;
            })
            .Run();

        Assert.That(toolNames, Is.Not.Null);
        Assert.That(toolNames, Has.Length.EqualTo(7), "Read-only mode should expose exactly 7 tools");

        Assert.That(toolNames, Does.Contain("get_errors_summary"));
        Assert.That(toolNames, Does.Contain("get_failed_messages"));
        Assert.That(toolNames, Does.Contain("get_failed_message_by_id"));
        Assert.That(toolNames, Does.Contain("get_failed_message_last_attempt"));
        Assert.That(toolNames, Does.Contain("get_failed_messages_by_endpoint"));
        Assert.That(toolNames, Does.Contain("get_failure_groups"));
        Assert.That(toolNames, Does.Contain("get_retry_history"));

        Assert.That(toolNames, Does.Not.Contain("retry_failed_message"));
        Assert.That(toolNames, Does.Not.Contain("retry_failed_messages"));
        Assert.That(toolNames, Does.Not.Contain("retry_failed_messages_by_queue"));
        Assert.That(toolNames, Does.Not.Contain("retry_all_failed_messages"));
        Assert.That(toolNames, Does.Not.Contain("retry_all_failed_messages_by_endpoint"));
        Assert.That(toolNames, Does.Not.Contain("retry_failure_group"));
        Assert.That(toolNames, Does.Not.Contain("archive_failed_message"));
        Assert.That(toolNames, Does.Not.Contain("archive_failed_messages"));
        Assert.That(toolNames, Does.Not.Contain("archive_failure_group"));
        Assert.That(toolNames, Does.Not.Contain("unarchive_failed_message"));
        Assert.That(toolNames, Does.Not.Contain("unarchive_failed_messages"));
        Assert.That(toolNames, Does.Not.Contain("unarchive_failure_group"));
    }
}
