namespace ServiceControl.Audit.AcceptanceTests.Mcp;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.EndpointTemplates;
using Audit.Auditing.MessagesView;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.Settings;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.AcceptanceTesting.Mcp;

class When_mcp_server_is_enabled : AcceptanceTest
{
    [SetUp]
    public void EnableMcp() => SetSettings = s => s.EnableMcpServer = true;

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
    public async Task Should_list_audit_message_tools()
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
        AssertAuditTools(sortedTools);
        McpAcceptanceTestSupport.AssertToolsHaveOutputSchema(sortedTools);
        var formattedTools = McpAcceptanceTestSupport.FormatToolsForApproval(sortedTools);
        Approver.Verify(formattedTools);
    }

    [Test]
    public async Task Should_call_get_audit_messages_tool()
    {
        string toolResult = null;

        var context = await Define<MyContext>()
            .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
            .WithEndpoint<Receiver>()
            .Done(async c =>
            {
                if (c.MessageId == null)
                {
                    return false;
                }

                // Wait for the message to be ingested
                if (!await this.TryGetMany<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId))
                {
                    return false;
                }

                var session = await InitializeAndGetSessionInfo();
                if (session == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(session, "tools/call", new
                {
                    name = "get_audit_messages",
                    arguments = new { includeSystemMessages = false, page = 1, perPage = 50 }
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
            Assert.That(structuredContent.GetProperty("totalCount").GetInt32(), Is.GreaterThanOrEqualTo(1));
            Assert.That(structuredContent.GetProperty("results").ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(structuredContent.GetProperty("results").GetArrayLength(), Is.GreaterThanOrEqualTo(1));
        });
    }

    static void AssertAuditTools(IReadOnlyCollection<JsonElement> tools)
    {
        Assert.That(tools, Has.Count.EqualTo(7));

        var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();

        Assert.That(names, Does.Contain("get_audit_messages"));
        Assert.That(names, Does.Contain("search_audit_messages"));
        Assert.That(names, Does.Contain("get_audit_message_body"));
        Assert.That(names, Does.Contain("get_known_endpoints"));
        Assert.That(names, Does.Contain("get_endpoint_audit_counts"));
    }

    Task<HttpResponseMessage> InitializeMcpSession() => McpAcceptanceTestSupport.InitializeMcpSession(HttpClient);

    Task<McpSessionInfo> InitializeAndGetSessionInfo() => McpAcceptanceTestSupport.InitializeAndGetSessionInfo(HttpClient);

    Task<HttpResponseMessage> SendMcpRequest(McpSessionInfo sessionInfo, string method, object @params) => McpAcceptanceTestSupport.SendMcpRequest(HttpClient, sessionInfo, method, @params);

    static Task<string> ReadMcpResponseJson(HttpResponseMessage response) => McpAcceptanceTestSupport.ReadMcpResponseJson(response);

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServerWithoutAudit>(c =>
            {
                var routing = c.ConfigureRouting();
                routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
            });
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServerWithAudit>();

        public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                testContext.MessageId = context.MessageId;
                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : ICommand;

    public class MyContext : ScenarioContext
    {
        public string MessageId { get; set; }
        public string EndpointNameOfReceivingEndpoint { get; set; }
    }
}
