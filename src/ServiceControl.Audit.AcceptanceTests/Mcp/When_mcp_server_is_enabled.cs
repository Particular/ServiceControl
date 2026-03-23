namespace ServiceControl.Audit.AcceptanceTests.Mcp;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
                var sessionId = await InitializeAndGetSessionId();
                if (sessionId == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(sessionId, "tools/list", new { });
                if (response == null)
                {
                    return false;
                }

                toolsJson = await ReadMcpResponseJson(response);
                return response.StatusCode == HttpStatusCode.OK;
            })
            .Run();

        Assert.That(toolsJson, Is.Not.Null);
        var doc = JsonDocument.Parse(toolsJson);
        var result = doc.RootElement.GetProperty("result");
        var tools = result.GetProperty("tools");
        var formattedTools = JsonSerializer.Serialize(tools, new JsonSerializerOptions { WriteIndented = true });
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

                var sessionId = await InitializeAndGetSessionId();
                if (sessionId == null)
                {
                    return false;
                }

                var response = await SendMcpRequest(sessionId, "tools/call", new
                {
                    name = "GetAuditMessages",
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
        var doc = JsonDocument.Parse(toolResult);
        var result = doc.RootElement.GetProperty("result");
        var content = result.GetProperty("content");
        var textContent = content.EnumerateArray().First().GetProperty("text").GetString();
        var messagesResult = JsonDocument.Parse(textContent);
        Assert.That(messagesResult.RootElement.GetProperty("totalCount").GetInt32(), Is.GreaterThanOrEqualTo(1));
    }

    async Task<HttpResponseMessage> InitializeMcpSession()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new { name = "test-client", version = "1.0" }
                }
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await HttpClient.SendAsync(request);
    }

    async Task<string> InitializeAndGetSessionId()
    {
        var response = await InitializeMcpSession();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        if (response.Headers.TryGetValues("mcp-session-id", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    static async Task<string> ReadMcpResponseJson(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType == "text/event-stream")
        {
            foreach (var line in body.Split('\n'))
            {
                if (line.StartsWith("data: "))
                {
                    return line.Substring("data: ".Length);
                }
            }
        }

        return body;
    }

    async Task<HttpResponseMessage> SendMcpRequest(string sessionId, string method, object @params)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 2,
                method,
                @params
            })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("mcp-session-id", sessionId);
        return await HttpClient.SendAsync(request);
    }

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
