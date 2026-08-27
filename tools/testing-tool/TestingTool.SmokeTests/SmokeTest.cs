using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TestingTool.SmokeTests;

/// <summary>
/// Smoke test for the ServiceControl testing tool. Requires a running ServiceControl instance
/// and testing tool (e.g. via docker-compose or the Aspire AppHost). The test:
/// 1. Verifies the testing tool is accessible
/// 2. Triggers the third-party-outage scenario for 30 seconds
/// 3. Verifies error groups appear in ServiceControl
/// 4. Triggers a replay and verifies it is accepted
///
/// Configure via environment variables:
///   TESTING_TOOL_URL (default: http://localhost:8080)
///   SERVICECONTROL_URL (default: http://localhost:33333)
/// </summary>
public class SmokeTest
{
    private static readonly string TestingToolUrl =
        Environment.GetEnvironmentVariable("TESTING_TOOL_URL") ?? "http://localhost:8080";
    private static readonly string ServiceControlUrl =
        Environment.GetEnvironmentVariable("SERVICECONTROL_URL") ?? "http://localhost:33333";

    private static readonly HttpClient ToolClient = new() { BaseAddress = new Uri(TestingToolUrl), Timeout = TimeSpan.FromSeconds(30) };
    private static readonly HttpClient ScClient = new() { BaseAddress = new Uri(ServiceControlUrl), Timeout = TimeSpan.FromSeconds(30) };

    [Fact]
    public async Task TestingTool_IsAccessible_ReturnsStatus()
    {
        var response = await ToolClient.GetAsync("/api/status");
        response.EnsureSuccessStatusCode();

        var status = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.Ready);
    }

    [Fact]
    public async Task ThirdPartyOutage_GeneratesErrors_VisibleInServiceControl()
    {
        // 1. Stop any existing scenario first (clean slate)
        await ToolClient.PostAsync("/api/scenarios/stop-all", null);

        // 2. Start the third-party-outage scenario for 30 seconds at 50 msg/s
        var startResponse = await ToolClient.PostAsJsonAsync(
            "/api/scenarios/third-party-outage/start",
            new { rate = 50, durationSeconds = 30 });
        startResponse.EnsureSuccessStatusCode();

        // 3. Wait for the scenario to generate errors (10s in, then check SC)
        await Task.Delay(TimeSpan.FromSeconds(10));

        // 4. Check ServiceControl for error groups
        var groupsResponse = await ScClient.GetAsync("/api/recoverability/groups");
        groupsResponse.EnsureSuccessStatusCode();

        var groups = await groupsResponse.Content.ReadFromJsonAsync<List<ErrorGroupResponse>>();
        Assert.NotNull(groups);
        Assert.NotEmpty(groups!);

        // 5. Wait for the scenario to finish (remaining ~20s + buffer)
        await Task.Delay(TimeSpan.FromSeconds(25));

        // 6. Verify the testing tool counted errors
        var statusResponse = await ToolClient.GetAsync("/api/status");
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(status);
        Assert.True(status!.ErrorsSent > 0, $"Expected errors to be sent, got {status.ErrorsSent}");
    }

    [Fact]
    public async Task Replay_ErrorGroup_IsAcceptedByServiceControl()
    {
        // 1. Ensure there are error groups to replay
        var groupsResponse = await ScClient.GetAsync("/api/recoverability/groups");
        groupsResponse.EnsureSuccessStatusCode();
        var groups = await groupsResponse.Content.ReadFromJsonAsync<List<ErrorGroupResponse>>();

        if (groups is null || groups.Count == 0)
        {
            // Generate some errors first
            await ToolClient.PostAsJsonAsync(
                "/api/scenarios/third-party-outage/start",
                new { rate = 50, durationSeconds = 10 });
            await Task.Delay(TimeSpan.FromSeconds(15));

            groupsResponse = await ScClient.GetAsync("/api/recoverability/groups");
            groupsResponse.EnsureSuccessStatusCode();
            groups = await groupsResponse.Content.ReadFromJsonAsync<List<ErrorGroupResponse>>();
        }

        Assert.NotNull(groups);
        Assert.NotEmpty(groups!);

        // 2. Trigger replay on the first group
        var firstGroup = groups![0];
        var replayResponse = await ScClient.PostAsJsonAsync(
            $"/api/recoverability/groups/{firstGroup.Id}/errors/retry", new { });

        // ServiceControl should accept the replay request (202 Accepted or 200 OK)
        Assert.True(replayResponse.IsSuccessStatusCode,
            $"Replay request failed: {replayResponse.StatusCode} {await replayResponse.Content.ReadAsStringAsync()}");
    }

    // --- Response DTOs (minimal, matching the APIs) ---

    private sealed record StatusResponse
    {
        public bool Ready { get; init; }
        public long ErrorsSent { get; init; }
        public long ErrorsReplayed { get; init; }
        public int ActiveScenarios { get; init; }
    }

    private sealed record ErrorGroupResponse
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public int Count { get; init; }
    }
}