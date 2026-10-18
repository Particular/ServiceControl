namespace ServiceControl.Federation.UnitTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

/// <summary>
/// Runs the proxy against two fake members over real HTTP and checks what ServicePulse would see.
/// </summary>
[TestFixture]
[NonParallelizable]
class FederationEndToEndTests
{
    [SetUp]
    public async Task SetUp()
    {
        memberA = await FakeMember.Start("a");
        memberB = await FakeMember.Start("b");
        (proxy, client) = await StartProxy(memberA, memberB);
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await proxy.DisposeAsync();
        await memberA.DisposeAsync();
        await memberB.DisposeAsync();
    }

    [Test]
    public async Task Failed_message_pages_are_merged_across_members()
    {
        memberA.Respond("GET", "/api/errors", ctx => List(ctx, Failures("a", 0, 2, 4)));
        memberB.Respond("GET", "/api/errors", ctx => List(ctx, Failures("b", 1, 3, 5)));

        var response = await client.GetAsync("api/errors?status=unresolved&page=2&per_page=2&sort=time_of_failure&direction=asc");
        var rows = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(rows.Select(r => r!["id"]!.GetValue<string>()), Is.EqualTo(new[] { "a-2", "b-3" }));
            Assert.That(response.Headers.GetValues("Total-Count").Single(), Is.EqualTo("6"));
            Assert.That(memberA.Requests, Has.Some.Contains("page=1").And.Contains("per_page=4"), "each member is asked for its first page·per_page rows");
        }
    }

    [Test]
    public async Task Batch_commands_reach_only_the_owning_member()
    {
        memberA.Respond("GET", "/api/errors", ctx => List(ctx, Failures("a", 0)));
        memberB.Respond("GET", "/api/errors", ctx => List(ctx, Failures("b", 1)));
        memberA.Respond("POST", "/api/errors/retry", _ => Results.Accepted());
        memberB.Respond("POST", "/api/errors/retry", _ => Results.Accepted());

        await client.GetAsync("api/errors");
        var response = await client.PostAsync("api/errors/retry", Json("[\"b-1\"]"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(memberB.Bodies("POST /api/errors/retry"), Is.EqualTo(new[] { "[\"b-1\"]" }));
            Assert.That(memberA.Bodies("POST /api/errors/retry"), Is.Empty);
        }
    }

    [Test]
    public async Task Unknown_ids_are_found_by_probing_members()
    {
        memberA.Respond("GET", "/api/errors/last/{id}", _ => Results.NotFound());
        memberB.Respond("GET", "/api/errors/last/{id}", _ => Results.Json(new { id = "b-9" }));
        memberB.Respond("PATCH", "/api/errors/archive", _ => Results.Accepted());

        var response = await client.PatchAsync("api/errors/archive", Json("[\"b-9\"]"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(memberB.Bodies("PATCH /api/errors/archive"), Has.Count.EqualTo(1));
            Assert.That(memberA.Requests, Has.None.StartsWith("PATCH"));
        }
    }

    [Test]
    public async Task Root_document_points_at_the_proxy_and_reports_the_lowest_version()
    {
        memberA.Version = "6.12.0";
        memberB.Version = "6.10.1";
        memberA.Respond("GET", "/api", ctx => Results.Json(new { name = "A", my_routes_url = $"{memberA.BaseUrl}api/my/routes", license_status = "valid" }));
        memberB.Respond("GET", "/api", ctx => Results.Json(new { name = "B", my_routes_url = $"{memberB.BaseUrl}api/my/routes", license_status = "valid" }));

        var response = await client.GetAsync("api");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Headers.GetValues("X-Particular-Version").Single(), Is.EqualTo("6.10.1"));
            Assert.That(body["my_routes_url"]!.GetValue<string>(), Is.EqualTo($"{client.BaseAddress}api/my/routes"));
            Assert.That(body["name"]!.GetValue<string>(), Is.EqualTo("Federation under test"));
        }
    }

    [Test]
    public async Task Message_bodies_are_routed_by_instance_id()
    {
        memberA.Respond("GET", "/api/messages", ctx => List(ctx, [new JsonObject { ["id"] = "m", ["message_id"] = "m", ["body_url"] = "/messages/m/body?instance_id=audit1" }]));
        memberB.Respond("GET", "/api/messages", ctx => List(ctx, []));
        memberA.Respond("GET", "/api/messages/{id}/body", ctx => Results.Text($"body from {ctx.Request.QueryString}", "text/plain"));

        var list = JsonNode.Parse(await client.GetStringAsync("api/messages"))!.AsArray();
        var bodyUrl = list[0]!["body_url"]!.GetValue<string>();
        var body = await client.GetStringAsync("api" + bodyUrl);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bodyUrl, Is.EqualTo("/messages/m/body?instance_id=a~audit1"));
            Assert.That(body, Is.EqualTo("body from ?instance_id=audit1"));
        }
    }

    [Test]
    public async Task Failure_groups_are_merged_by_id()
    {
        memberA.Respond("GET", "/api/recoverability/groups/{classifier}", _ => Results.Json(new[] { new { id = "g", title = "T", count = 2, last = "2024-01-01T00:00:00Z", operation_status = "None" } }));
        memberB.Respond("GET", "/api/recoverability/groups/{classifier}", _ => Results.Json(new[] { new { id = "g", title = "T", count = 3, last = "2024-02-01T00:00:00Z", operation_status = "None" } }));

        var groups = JsonNode.Parse(await client.GetStringAsync("api/recoverability/groups/Exception%20Type%20and%20Stack%20Trace"))!.AsArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0]!["count"]!.GetValue<long>(), Is.EqualTo(5));
            Assert.That(memberA.Requests, Has.Some.Contains("Exception%20Type%20and%20Stack%20Trace"), "the path is forwarded as the client encoded it");
        }
    }

    [Test]
    public async Task An_unreachable_member_is_reported_as_incomplete()
    {
        memberA.Respond("GET", "/api/customchecks", ctx => List(ctx, []));
        await memberB.DisposeAsync();

        var response = await client.GetAsync("api/customchecks?status=fail");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.GetValues("X-Particular-Incomplete-Results").Single(), Does.EndWith(":unavailable"));
        }
    }

    static IResult List(HttpContext context, IEnumerable<JsonNode> rows)
    {
        var all = rows.ToList();
        var page = int.TryParse(context.Request.Query["page"], out var p) ? p : 1;
        var perPage = int.TryParse(context.Request.Query["per_page"], out var pp) ? pp : 50;
        context.Response.Headers["Total-Count"] = all.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Content(new JsonArray([.. all.Skip((page - 1) * perPage).Take(perPage)]).ToJsonString(), "application/json");
    }

    static IEnumerable<JsonNode> Failures(string member, params int[] minutes) =>
        minutes.Select(minute => (JsonNode)new JsonObject
        {
            ["id"] = $"{member}-{minute}",
            ["message_id"] = $"{member}-{minute}",
            ["time_of_failure"] = DateTime.UnixEpoch.AddMinutes(minute).ToString("O")
        });

    static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    static async Task<(WebApplication, HttpClient)> StartProxy(params FakeMember[] members)
    {
        // The tests address the federated surfaces at /api and /monitoring for readability; a hosting
        // primary uses FederationSettings.DefaultPrimaryPathBase and DefaultMonitoringPathBase.
        var settings = new FederationSettings
        {
            DisplayName = "Federation under test",
            MemberTimeout = TimeSpan.FromSeconds(5),
            HealthProbeInterval = TimeSpan.FromHours(1),
            PrimaryPathBase = "/api",
            MonitoringPathBase = "/monitoring",
            Members = [.. members.Select(m => new MemberSettings { Key = m.Key, PrimaryUrl = $"{m.BaseUrl}api/", MonitoringUrl = m.BaseUrl })]
        };

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddServiceControlFederation(settings);

        var app = builder.Build();
        app.UseRouting();
        app.MapServiceControlFederation();
        await app.StartAsync();

        var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        return (app, client);
    }

    static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/') + "/";

    FakeMember memberA = null!;
    FakeMember memberB = null!;
    WebApplication proxy = null!;
    HttpClient client = null!;

    sealed class FakeMember : IAsyncDisposable
    {
        FakeMember(string key) => Key = key;

        public string Key { get; }

        public string BaseUrl { get; private set; } = "";

        public string Version { get; set; } = "6.12.0";

        public ConcurrentQueue<string> Requests { get; } = new();

        public List<string> Bodies(string methodAndPath) =>
            [.. bodies.Where(b => b.Key == methodAndPath).Select(b => b.Value)];

        public void Respond(string method, string path, Func<HttpContext, IResult> handler) =>
            handlers.Enqueue((method, new TemplateMatcher(TemplateParser.Parse(path.TrimStart('/')), []), handler));

        public static async Task<FakeMember> Start(string key)
        {
            var member = new FakeMember(key);
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Run(async context =>
            {
                member.Requests.Enqueue($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
                if (context.Request.ContentLength > 0)
                {
                    using var reader = new System.IO.StreamReader(context.Request.Body);
                    member.bodies.Add(new KeyValuePair<string, string>($"{context.Request.Method} {context.Request.Path}", await reader.ReadToEndAsync()));
                }

                context.Response.Headers["X-Particular-Version"] = member.Version;
                foreach (var (method, matcher, handler) in member.handlers)
                {
                    if (method == context.Request.Method && matcher.TryMatch(context.Request.Path, context.Request.RouteValues))
                    {
                        await handler(context).ExecuteAsync(context);
                        return;
                    }
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
            member.app = app;
            await app.StartAsync();
            member.BaseUrl = Address(app);
            return member;
        }

        public async ValueTask DisposeAsync()
        {
            if (app != null)
            {
                var stopping = app;
                app = null;
                await stopping.DisposeAsync();
            }
        }

        readonly ConcurrentBag<KeyValuePair<string, string>> bodies = [];
        readonly ConcurrentQueue<(string Method, TemplateMatcher Matcher, Func<HttpContext, IResult> Handler)> handlers = new();
        WebApplication? app;
    }
}
