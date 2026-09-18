#nullable enable
namespace ServiceControl.AcceptanceTests.Mcp;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceControl.AcceptanceTesting.Mcp;

[TestFixture]
class When_mcp_server_write_mode_is_enabled : AcceptanceTest
{
    [SetUp]
    public void EnableWriteMode() => SetSettings = s => s.EnableMcpServerWriteMode = true;

    [Test]
    public async Task Should_expose_write_tools()
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
                if (response == null || response.StatusCode != HttpStatusCode.OK)
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
        Assert.That(toolNames, Does.Contain("retry_failed_message"));
        Assert.That(toolNames, Does.Contain("retry_failure_group"));
        Assert.That(toolNames, Has.Length.EqualTo(10));
    }
}