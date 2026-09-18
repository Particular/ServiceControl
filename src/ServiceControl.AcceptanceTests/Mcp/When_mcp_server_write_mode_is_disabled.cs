#nullable enable
namespace ServiceControl.AcceptanceTests.Mcp;

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
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
        string?[]? toolNames = null;

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
        Assert.That(toolNames, Does.Not.Contain("retry_failed_message"));
        Assert.That(toolNames, Does.Not.Contain("retry_failure_group"));
    }
}