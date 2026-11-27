using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NServiceBus;
using ServiceControl.Plugin.CustomChecks.Messages;
using static TestingTool.Smoke.SmokeApiClient;

namespace TestingTool.Smoke;

/// <summary>
/// The smoke checklist. Every automated check sends its own messages with a run-scoped identifier, then
/// reads the effect back through the ServiceControl APIs. Nothing is matched by anything the background
/// load could also produce.
/// </summary>
public sealed class SmokeChecks(
    SmokeApiClient api,
    SmokeMonitoringClient monitoring,
    IMessageSession session,
    SmokeObservations observations,
    IOptions<TestingToolOptions> options,
    IConfiguration configuration)
{
    /// <summary>Heartbeats are marked dead 40 seconds after the last one, checked every 5 seconds.</summary>
    static readonly TimeSpan HeartbeatDeadTimeout = TimeSpan.FromSeconds(100);

    public IReadOnlyList<SmokeCheck> Create() =>
    [
        new("custom-check-fail-then-pass", "Custom check fails, then passes",
            "Reports a failed then a passed custom check with a run-specific id to the primary input queue, and reads it from GET /api/customchecks?status=fail and ?status=pass.",
            CustomCheckFailThenPass),
        new("heartbeat-beating-then-dead", "Heartbeat is healthy, then the endpoint is reported dead",
            "Starts a real endpoint named after the run with the NServiceBus.Heartbeat plugin and reads heartbeat_information.reported_status from GET /api/endpoints: beating, then dead after the endpoint host stops. Takes about a minute.",
            HeartbeatBeatingThenDead, TimeSpan.FromMinutes(4)),
        new("audit-ingestion-fulltext", "Audited message is ingested and found by full-text search",
            "Processes a message carrying a run-specific word, then finds it with GET /api/messages/search and reads its body from GET /api/messages/{id}/body. Needs an audit instance registered as a remote of the primary.",
            AuditIngestionFulltext),
        new("fanout-conversation", "Fan-out conversation keeps its parent links",
            "Processes a message that sends two children per level for two levels, then reads the seven-message conversation from GET /api/conversations/{id} and checks each NServiceBus.RelatedTo header. Needs an audit instance.",
            FanOutConversation),
        new("saga-audit", "Saga audit data records new, updated and completed",
            "Runs a real saga through three state changes with saga auditing on, finds the audited start message with GET /api/messages/search and reads GET /api/sagas/{id}. Needs an audit instance.",
            SagaAudit),
        new("monitoring-data", "Monitoring reports processing time for an endpoint",
            "Starts a real endpoint named after the run with the NServiceBus.Metrics.ServiceControl plugin, handles three messages that each take 100 ms, and reads the processingTime average for that endpoint from GET /monitored-endpoints on the monitoring instance.",
            MonitoringData),
        new("integration-event", "MessageFailed integration event is received",
            "Subscribes this endpoint to ServiceControl.Contracts.MessageFailed, fails one message, and matches the published event by a run header and the failed message id. Needs SERVICECONTROL_ENABLEEXTERNALINTEGRATIONSPUBLISHING=true on the instance that ingests errors.",
            IntegrationEvent),
        new("retry-single", "Retry one failed message",
            "Fails two messages in a run-specific group, retries one with POST /api/errors/{id}/retry, and checks that it was handled and resolved while the other stayed unresolved.",
            RetrySingle),
        new("retry-group", "Retry a failed message group",
            "Fails three messages in a run-specific group, retries the group with POST /api/recoverability/groups/{id}/errors/retry, and checks that each message was handled and resolved.",
            RetryGroup),
        new("edit-and-retry", "Edit the body of a failed message and retry it",
            "Fails a message, sends the edited body that no longer fails through POST /api/edit/{id} with every original header, and checks that the edited message arrived with ServiceControl.EditOf and the edited text. Needs SERVICECONTROL_ALLOWMESSAGEEDITING=true.",
            EditAndRetry),
        new("retry-through-redirect", "Retry through a redirect",
            "Starts a Source and a Target endpoint, fails a message in Source, creates a redirect from the Source input address to the Target input address with POST /api/redirects, retries it, and checks that Target handled it and it is resolved. Runs last because a redirect changes retry routing while it exists.",
            RetryThroughRedirect),

        SmokeCheck.Manual("manual-installation", "Instance installation and upgrade",
            "Not automated. Upgrade and create instances with the installer and with the PowerShell scripts, on localhost and on a custom hostname, as listed under Instance management installation."),
        SmokeCheck.Manual("manual-servicepulse-custom-checks-heartbeats", "ServicePulse: custom checks and heartbeats views",
            "Not automated. The API data is verified above. A person confirms that ServicePulse shows the custom check warning and clears it, and moves an endpoint from healthy to unhealthy."),
        SmokeCheck.Manual("manual-servicepulse-recoverability", "ServicePulse: recoverability screens",
            "Not automated. The API effects are verified above. A person confirms the retry, edit and redirect screens in ServicePulse, including Configuration > Create redirect."),
        SmokeCheck.Manual("manual-servicepulse-audit-saga", "ServicePulse: message search, sequence diagram and saga view",
            "Not automated. The audit, conversation and saga data is verified above. A person confirms that the search box finds a message, that the sequence diagram draws the fan-out, and that the saga view draws the graph."),
        SmokeCheck.Manual("manual-integration-events-sample", "Integration events sample",
            "Not automated. This tool verifies reception in its own endpoint. A person runs the public integration events sample on the transport under test and confirms that its subscriber receives MessageFailed."),
        SmokeCheck.Manual("manual-monitoring-graphs", "ServicePulse: monitoring tab",
            "Not automated. The metric data is verified above for a real run-scoped endpoint. A person confirms that the endpoints, the failed messages indicator and the graphs render."),
        SmokeCheck.Manual("manual-chaos", "Chaos testing",
            "Not automated. Stop ServiceControl processes gracefully and with kill while ingestion and large retry groups run, and verify storage and logic afterwards."),
        SmokeCheck.Manual("manual-performance", "Performance, load and stability testing",
            "Not automated. Use the load scenarios and bypass writers for performance on clean, moderate and large databases, and check reboots, service stops, virtual machine kills and upgrades under load.")
    ];

    async Task CustomCheckFailThenPass(SmokeContext ctx, CancellationToken ct)
    {
        var checkId = $"Smoke.{ctx.RunId}";
        var hostId = Guid.NewGuid();
        var reason = $"Smoke run {ctx.RunId} reports a failure";
        ctx.OnCleanup(async token =>
        {
            foreach (var status in new[] { "fail", "pass" })
            {
                foreach (var view in (await api.GetCustomChecks(status, token)).Where(c => c.CustomCheckId == checkId))
                {
                    await api.DeleteCustomCheck(view.Id, token);
                }
            }
        });

        await SendCustomCheck(checkId, hostId, ctx.RunId, failed: true, reason, ct);
        await ctx.WaitFor("the custom check to appear as failed", async token =>
        {
            var checks = (await api.GetCustomChecks("fail", token)).Where(c => c.CustomCheckId == checkId).ToList();
            return checks.Count == 0
                ? PollResult.Waiting<CustomCheckView>($"no failed check with id {checkId}")
                : checks[0].FailureReason == reason
                    ? PollResult.Done(checks[0])
                    : PollResult.Waiting<CustomCheckView>($"failure reason was '{checks[0].FailureReason}'");
        }, ct);

        await SendCustomCheck(checkId, hostId, ctx.RunId, failed: false, null, ct);
        await ctx.WaitFor("the custom check to pass and leave the failed list", async token =>
        {
            var passed = (await api.GetCustomChecks("pass", token)).Any(c => c.CustomCheckId == checkId);
            var stillFailed = (await api.GetCustomChecks("fail", token)).Any(c => c.CustomCheckId == checkId);
            return passed && !stillFailed ? PollResult.Done(true) : PollResult.Waiting<bool>($"passed list has it: {passed}, failed list has it: {stillFailed}");
        }, ct);

        ctx.Detail = $"Custom check {checkId} was reported failed, then passed.";
    }

    Task SendCustomCheck(string checkId, Guid hostId, string runId, bool failed, string? reason, CancellationToken ct)
    {
        var send = new SendOptions();
        send.SetDestination(options.Value.ServiceControlInputQueue);
        return session.Send(new ReportCustomCheckResult
        {
            HostId = hostId,
            CustomCheckId = checkId,
            Category = "Smoke",
            HasFailed = failed,
            FailureReason = reason,
            ReportedAt = DateTime.UtcNow,
            EndpointName = $"TestingTool.Smoke.{runId}",
            Host = "TestingTool.Smoke"
        }, send, ct);
    }

    async Task HeartbeatBeatingThenDead(SmokeContext ctx, CancellationToken ct)
    {
        var name = $"TestingTool.Smoke.{ctx.RunId}";
        DeleteEndpointRecords(ctx, name);

        var endpoint = await StartEndpoint(ctx, name, config => config.SendHeartbeatTo(options.Value.ServiceControlInputQueue), ct);
        await ctx.WaitFor("the endpoint to report beating", token => EndpointStatus(name, "beating", token), ct);
        var beating = DateTime.UtcNow;

        await endpoint.StopAsync(ct);
        await ctx.WaitFor("the endpoint to report dead after it stopped", token => EndpointStatus(name, "dead", token), ct, HeartbeatDeadTimeout);

        ctx.Detail = $"Endpoint {name} sent heartbeats with the NServiceBus.Heartbeat plugin, was reported beating, and was reported dead {(DateTime.UtcNow - beating).TotalSeconds:0}s after its host stopped.";
    }

    /// <summary>Deletes the primary instance's records of exactly these endpoint names, and no others. It runs after the endpoint hosts stop.</summary>
    public void DeleteEndpointRecords(SmokeContext ctx, params string[] names) =>
        ctx.OnCleanup(async token =>
        {
            foreach (var view in (await api.GetEndpoints(token)).Where(e => names.Contains(e.Name)))
            {
                await api.DeleteEndpoint(view.Id, token);
            }
        });

    /// <summary>Starts a run-scoped endpoint on the transport of the load endpoint. The cleanup stops it if the check did not.</summary>
    async Task<SmokeEndpoint> StartEndpoint(SmokeContext ctx, string name, Action<EndpointConfiguration>? configure, CancellationToken ct)
    {
        var endpoint = await SmokeEndpoint.Start(name, config => NServiceBusEndpointExtensions.ConfigureTransport(config, configuration), configure, observations, options.Value.ErrorQueueName, ct);
        ctx.OnEndpointStop(token => endpoint.StopAsync(token));
        return endpoint;
    }

    async Task<PollResult<bool>> EndpointStatus(string endpoint, string expected, CancellationToken ct)
    {
        var view = (await api.GetEndpoints(ct)).FirstOrDefault(e => e.Name == endpoint);
        var status = view?.HeartbeatInformation?.ReportedStatus;
        return view is { MonitorHeartbeat: true } && string.Equals(status, expected, StringComparison.OrdinalIgnoreCase)
            ? PollResult.Done(true)
            : PollResult.Waiting<bool>(view is null ? $"endpoint {endpoint} is not listed" : $"reported_status is '{status}', monitor_heartbeat is {view.MonitorHeartbeat}");
    }

    async Task AuditIngestionFulltext(SmokeContext ctx, CancellationToken ct)
    {
        await RequireAuditInstance(ct);
        var token = Token("smokeaudit");
        var messageId = Guid.NewGuid().ToString();
        await SendSmoke(ctx, new SmokeMessage { RunId = ctx.RunId, Marker = messageId, Note = $"full text {token}" }, messageId, ct: ct);

        var audited = await ctx.WaitFor("the audited message to be found by search", async _ =>
        {
            var result = await api.SearchMessages(token, ct);
            var match = result.Value.FirstOrDefault(m => m.MessageId == messageId);
            return match is not null ? PollResult.Done(match) : PollResult.Waiting<AuditedMessage>(Describe(result, $"{result.Value.Count} results for '{token}', none with message id {messageId}"));
        }, ct);

        if (!string.Equals(audited.Status, "successful", StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeCheckFailedException($"The audited message has status '{audited.Status}', expected successful");
        }

        var body = await api.GetMessageBody(audited, ct);
        if (!body.Contains(token, StringComparison.Ordinal))
        {
            throw new SmokeCheckFailedException($"The stored body of audited message {audited.Id} does not contain '{token}'");
        }

        ctx.Detail = $"Message {messageId} was found by searching '{token}' and its body contains the word.";
    }

    async Task FanOutConversation(SmokeContext ctx, CancellationToken ct)
    {
        await RequireAuditInstance(ct);
        var conversationId = Guid.NewGuid().ToString();
        var rootId = Guid.NewGuid().ToString();
        var expected = (1 << (SmokeFanOutHandler.Levels + 1)) - 1;
        await SendSmoke(ctx, new SmokeFanOutMessage(), rootId, send => send.SetHeader(Headers.ConversationId, conversationId), ct: ct);

        var messages = await ctx.WaitFor($"{expected} audited messages in the conversation", async _ =>
        {
            var result = await api.GetConversation(conversationId, ct);
            return result.Value.Count >= expected ? PollResult.Done(result.Value) : PollResult.Waiting<List<AuditedMessage>>(Describe(result, $"{result.Value.Count} messages"));
        }, ct);

        var ids = messages.Select(m => m.MessageId).ToHashSet();
        var parentOf = messages.ToDictionary(m => m.MessageId, m => m.Header(Headers.RelatedTo));
        var problems = new List<string>();
        if (messages.Count != expected)
        {
            problems.Add($"expected {expected} messages, found {messages.Count}");
        }

        if (!parentOf.TryGetValue(rootId, out var rootParent) || rootParent is not null)
        {
            problems.Add("the root message is missing or has a parent");
        }

        problems.AddRange(parentOf.Where(p => p.Key != rootId && (p.Value is null || !ids.Contains(p.Value))).Select(p => $"message {p.Key} has RelatedTo '{p.Value}' that is not in the conversation"));
        var rootChildren = parentOf.Count(p => p.Value == rootId);
        if (rootChildren != 2)
        {
            problems.Add($"the root has {rootChildren} children, expected 2");
        }

        if (problems.Count > 0)
        {
            throw new SmokeCheckFailedException($"Conversation {conversationId} is wrong: {string.Join("; ", problems)}");
        }

        ctx.Detail = $"Conversation {conversationId} has {messages.Count} messages, each linked to its parent.";
    }

    async Task SagaAudit(SmokeContext ctx, CancellationToken ct)
    {
        await RequireAuditInstance(ct);
        var correlationId = Token("smokesaga");
        await session.Send(new SmokeSagaStart { CorrelationId = correlationId }, RouteHere(ctx.RunId), ct);

        var sagaId = await ctx.WaitFor("the audited saga start message with its saga id", async _ =>
        {
            var result = await api.SearchMessages(correlationId, ct);
            var saga = result.Value.Select(m => m.InvokedSagas?.FirstOrDefault()).FirstOrDefault(s => s is not null);
            return saga is not null ? PollResult.Done(saga) : PollResult.Waiting<SagaReference>(Describe(result, $"{result.Value.Count} results for '{correlationId}', none with an invoked saga"));
        }, ct);

        var history = await ctx.WaitFor("three saga state changes", async _ =>
        {
            var result = await api.GetSaga(sagaId.SagaId, ct);
            var changes = result.Value?.Changes.Count ?? 0;
            return changes >= 3 ? PollResult.Done(result.Value!) : PollResult.Waiting<SagaHistory>(Describe(result, $"{changes} changes"));
        }, ct);

        var statuses = history.Changes.OrderBy(c => c.FinishTime).Select(c => c.Status.ToLowerInvariant()).ToArray();
        if (!statuses.SequenceEqual(["new", "updated", "completed"]))
        {
            throw new SmokeCheckFailedException($"Saga {sagaId.SagaId} has state changes [{string.Join(", ", statuses)}], expected [new, updated, completed]");
        }

        ctx.Detail = $"Saga {sagaId.SagaId} ({history.SagaType}) recorded new, updated and completed.";
    }

    async Task MonitoringData(SmokeContext ctx, CancellationToken ct)
    {
        var name = $"TestingTool.Smoke.{ctx.RunId}";
        ctx.OnCleanup(async token =>
        {
            // The instance ids are the ones monitoring reports for the endpoint, after the host stopped and reports no more.
            var reported = (await monitoring.GetMonitoredEndpoints(token)).FirstOrDefault(e => e.Name == name);
            foreach (var instanceId in reported?.EndpointInstanceIds ?? [])
            {
                await monitoring.DeleteInstance(name, instanceId, token);
            }
        });

        var endpoint = await StartEndpoint(ctx, name, config => config.EnableMetrics().SendMetricDataToServiceControl(options.Value.MonitoringQueueName, TimeSpan.FromSeconds(2)), ct);
        for (var i = 0; i < 3; i++)
        {
            await SendSmoke(ctx, new SmokeMessage { RunId = ctx.RunId, Marker = $"{ctx.RunId}-monitoring-{i}", Note = "monitoring", DelayMilliseconds = 100 }, destination: endpoint.MainAddress, ct: ct);
        }

        var average = await ctx.WaitFor("monitoring to report processing time for the endpoint", async token =>
        {
            var found = (await monitoring.GetMonitoredEndpoints(token)).FirstOrDefault(e => e.Name == name);
            var value = found?.Metrics.FirstOrDefault(m => string.Equals(m.Key, "processingTime", StringComparison.OrdinalIgnoreCase)).Value?.Average;
            return value > 0 ? PollResult.Done(value.Value) : PollResult.Waiting<double>(found is null ? $"endpoint {name} is not listed" : "processingTime has no average yet");
        }, ct);

        ctx.Detail = $"Endpoint {name} reported an average processing time of {average:0.#} ms with the NServiceBus.Metrics.ServiceControl plugin.";
    }

    async Task IntegrationEvent(SmokeContext ctx, CancellationToken ct)
    {
        await session.Subscribe<ServiceControl.Contracts.MessageFailed>(ct);
        ctx.OnCleanup(token => session.Unsubscribe<ServiceControl.Contracts.MessageFailed>(token));

        var failures = await FailMessages(ctx, "event", 1, ct: ct);
        var failed = (await WaitForFailures(ctx, failures, ct)).Messages[0];
        var eventFailedId = await ctx.WaitFor(
            "the MessageFailed event (the instance that ingests errors needs SERVICECONTROL_ENABLEEXTERNALINTEGRATIONSPUBLISHING=true)",
            _ => Task.FromResult(observations.FindFailureEvent(failures.MessageIds[0]) is { } id ? PollResult.Done(id) : PollResult.Waiting<string>($"no event for message id {failures.MessageIds[0]}")),
            ct);

        if (eventFailedId != failed.Id)
        {
            throw new SmokeCheckFailedException($"The MessageFailed event carries failed message id {eventFailedId}, the API lists {failed.Id}");
        }

        ctx.Detail = $"MessageFailed was received for failed message {failed.Id}.";
    }

    async Task RetrySingle(SmokeContext ctx, CancellationToken ct)
    {
        var failures = await FailMessages(ctx, "retry-single", 2, ct: ct);
        var group = await WaitForFailures(ctx, failures, ct);
        failures.Markers.ToList().ForEach(observations.Release);

        var retried = group.Messages[0];
        var untouched = group.Messages[1];
        await api.RetryMessage(retried.Id, ct);
        await WaitUntilRetried(ctx, retried, failures.MarkerOf(retried), ct);

        var other = await api.FindFailedMessage(untouched.Id, ct);
        if (!string.Equals(other?.Status, "unresolved", StringComparison.OrdinalIgnoreCase) || observations.FindHandled(failures.MarkerOf(untouched)) is not null)
        {
            throw new SmokeCheckFailedException($"Retrying one message also acted on its sibling {untouched.Id}: status '{other?.Status}'");
        }

        ctx.Detail = $"Message {retried.Id} was handled and resolved. Its sibling {untouched.Id} stayed unresolved.";
    }

    async Task RetryGroup(SmokeContext ctx, CancellationToken ct)
    {
        var failures = await FailMessages(ctx, "retry-group", 3, ct: ct);
        var group = await WaitForFailures(ctx, failures, ct);
        failures.Markers.ToList().ForEach(observations.Release);

        await api.RetryGroup(group.GroupId, ct);
        foreach (var message in group.Messages)
        {
            await WaitUntilRetried(ctx, message, failures.MarkerOf(message), ct);
        }

        ctx.Detail = $"All {group.Messages.Count} messages of group {group.GroupId} were handled and resolved.";
    }

    async Task EditAndRetry(SmokeContext ctx, CancellationToken ct)
    {
        var failures = await FailMessages(ctx, "edit", 1, ct: ct);
        var failed = (await WaitForFailures(ctx, failures, ct)).Messages[0];
        var detail = await api.FindFailedMessage(failed.Id, ct) ?? throw new SmokeCheckFailedException($"Failed message {failed.Id} disappeared");

        var token = Token("smokeedit");
        var editedMarker = $"{failures.MarkerOf(failed)}-edited";
        var body = JsonSerializer.Serialize(new SmokeMessage { RunId = ctx.RunId, Marker = editedMarker, Note = $"edited {token}", Fail = false });
        var accepted = await api.EditMessage(failed.Id, body, new Dictionary<string, string>(detail.ProcessingAttempts[^1].Headers), ct);
        if (!accepted)
        {
            throw new SmokeCheckFailedException($"ServiceControl ignored the edit of {failed.Id} (edit_ignored)");
        }

        var handled = await ctx.WaitFor("the edited message to be handled", _ =>
            Task.FromResult(observations.FindHandled(editedMarker) is { } h ? PollResult.Done(h) : PollResult.Waiting<SmokeObservations.HandledMessage>("the edited message has not arrived")), ct);

        if (handled.EditOf != failed.Id || !handled.Note.Contains(token, StringComparison.Ordinal))
        {
            throw new SmokeCheckFailedException($"The edited message arrived with EditOf '{handled.EditOf}' and note '{handled.Note}', expected EditOf {failed.Id} and the edited text");
        }

        ctx.Detail = $"The edited body of {failed.Id} arrived with ServiceControl.EditOf and the edited text, and no longer failed.";
    }

    async Task RetryThroughRedirect(SmokeContext ctx, CancellationToken ct)
    {
        var sourceName = $"TestingTool.Smoke.{ctx.RunId}.Source";
        var targetName = $"TestingTool.Smoke.{ctx.RunId}.Target";
        // The failure that Source reports makes ServiceControl save an endpoint record for it.
        DeleteEndpointRecords(ctx, sourceName, targetName);
        var source = await StartEndpoint(ctx, sourceName, null, ct);
        var target = await StartEndpoint(ctx, targetName, null, ct);
        var from = source.MainAddress;
        var to = target.MainAddress;

        var failures = await FailMessages(ctx, "redirect", 1, source.MainAddress, ct);
        var failed = (await WaitForFailures(ctx, failures, ct)).Messages[0];
        if (failed.QueueAddress != from)
        {
            throw new SmokeCheckFailedException($"The failed message has queue '{failed.QueueAddress}', expected the source address '{from}'");
        }

        var marker = failures.MarkerOf(failed);
        observations.Release(marker);

        ctx.OnCleanup(async token =>
        {
            foreach (var redirect in (await api.GetRedirects(token)).Where(r => r.FromPhysicalAddress == from))
            {
                await api.DeleteRedirect(redirect.MessageRedirectId, token);
            }
        });

        await api.CreateRedirect(from, to, ct);
        var listed = await ctx.WaitFor("the redirect to be listed", async token =>
        {
            var found = (await api.GetRedirects(token)).FirstOrDefault(r => r.FromPhysicalAddress == from && r.ToPhysicalAddress == to);
            return found is not null ? PollResult.Done(found) : PollResult.Waiting<RedirectView>($"no redirect from {from} to {to}");
        }, ct);

        await api.RetryMessage(failed.Id, ct);
        await WaitUntilRetried(ctx, failed, marker, ct);
        RequireHandledBy(observations.FindHandled(marker), target.Name, source.Name);

        await api.DeleteRedirect(listed.MessageRedirectId, ct);
        var remaining = (await api.GetRedirects(ct)).Count(r => r.FromPhysicalAddress == from);
        if (remaining != 0)
        {
            throw new SmokeCheckFailedException($"The redirect from {from} is still listed after DELETE");
        }

        ctx.Detail = $"A message that failed in {source.Name} ({from}) was retried through a redirect to {target.Name} ({to}), handled there and resolved. The redirect was removed.";
    }

    /// <summary>A retry that the source handled itself means the redirect did nothing, so only the target passes.</summary>
    public static void RequireHandledBy(SmokeObservations.HandledMessage? handled, string target, string source)
    {
        if (handled?.Endpoint != target)
        {
            throw new SmokeCheckFailedException($"The retried message was handled by '{handled?.Endpoint ?? "no endpoint"}', expected the redirect target '{target}' and not the source '{source}'");
        }
    }

    // --- Shared steps ---

    sealed record Failures(string RunId, string ExceptionType, IReadOnlyList<string> MessageIds, IReadOnlyList<string> Markers)
    {
        public string MarkerOf(FailedMessageView view) => Markers[MessageIds.ToList().IndexOf(view.MessageId)];
    }

    sealed record FailureGroup(string GroupId, IReadOnlyList<FailedMessageView> Messages);

    /// <summary>
    /// Sends failing messages that ServiceControl groups under an exception type unique to this check of
    /// this run. The group therefore holds only these messages, so a group retry cannot touch anything else.
    /// </summary>
    async Task<Failures> FailMessages(SmokeContext ctx, string checkId, int count, string? destination = null, CancellationToken ct = default)
    {
        var exceptionType = $"TestingTool.Smoke.Failure_{ctx.RunId}_{checkId}".Replace('-', '_');
        var messageIds = new List<string>();
        var markers = new List<string>();
        var failures = new Failures(ctx.RunId, exceptionType, messageIds, markers);
        ctx.OnCleanup(token => ArchiveLeftovers(failures, token));

        for (var i = 0; i < count; i++)
        {
            var messageId = Guid.NewGuid().ToString();
            var marker = $"{ctx.RunId}-{checkId}-{i}";
            messageIds.Add(messageId);
            markers.Add(marker);
            await SendSmoke(ctx, new SmokeMessage
            {
                RunId = ctx.RunId,
                Marker = marker,
                Fail = true,
                Note = checkId,
                FailureType = exceptionType
            }, messageId, destination: destination, ct: ct);
        }

        return failures;
    }

    async Task<FailureGroup> WaitForFailures(SmokeContext ctx, Failures failures, CancellationToken ct)
    {
        var expected = failures.MessageIds.Count;
        return await ctx.WaitFor($"{expected} failed message(s) in the group for {failures.ExceptionType}", async token =>
        {
            var group = (await api.GetGroups(token)).FirstOrDefault(g => g.Title.StartsWith(failures.ExceptionType + ":", StringComparison.Ordinal));
            if (group is null)
            {
                return PollResult.Waiting<FailureGroup>("the group does not exist yet");
            }

            var mine = (await api.GetGroupErrors(group.Id, token)).Where(m => failures.MessageIds.Contains(m.MessageId)).ToList();
            return mine.Count == expected && mine.All(m => string.Equals(m.Status, "unresolved", StringComparison.OrdinalIgnoreCase))
                ? PollResult.Done(new FailureGroup(group.Id, mine))
                : PollResult.Waiting<FailureGroup>($"group {group.Id} lists {mine.Count} of the run's messages: [{string.Join(", ", mine.Select(m => m.Status))}]");
        }, ct);
    }

    Task WaitUntilRetried(SmokeContext ctx, FailedMessageView message, string marker, CancellationToken ct) =>
        ctx.WaitFor($"failed message {message.Id} to be handled and resolved", async token =>
        {
            var handled = observations.FindHandled(marker) is not null;
            var detail = await api.FindFailedMessage(message.Id, token);
            return handled && string.Equals(detail?.Status, "resolved", StringComparison.OrdinalIgnoreCase)
                ? PollResult.Done(true)
                : PollResult.Waiting<bool>($"handled by the endpoint: {handled}, status: {detail?.Status}");
        }, ct);

    async Task ArchiveLeftovers(Failures failures, CancellationToken ct)
    {
        var group = (await api.GetGroups(ct)).FirstOrDefault(g => g.Title.StartsWith(failures.ExceptionType + ":", StringComparison.Ordinal));
        if (group is null)
        {
            return;
        }

        var leftovers = (await api.GetGroupErrors(group.Id, ct))
            .Where(m => failures.MessageIds.Contains(m.MessageId) && string.Equals(m.Status, "unresolved", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Id)
            .ToList();
        if (leftovers.Count > 0)
        {
            await api.ArchiveMessages(leftovers, ct);
        }
    }

    async Task RequireAuditInstance(CancellationToken ct)
    {
        var remotes = await api.GetRemotes(ct);
        if (remotes.Count == 0)
        {
            throw new SmokeCheckFailedException("The primary instance has no remote audit instance, so it cannot return audit data. Start the AppHost with --audit-instances 1.");
        }

        var offline = remotes.Where(r => !string.Equals(r.Status, "online", StringComparison.OrdinalIgnoreCase)).ToList();
        if (offline.Count > 0)
        {
            throw new SmokeCheckFailedException($"Remote instance(s) not online: {string.Join(", ", offline.Select(r => $"{r.ApiUri} is {r.Status}"))}");
        }
    }

    static string Describe<T>(Audited<T> result, string observed) =>
        result.Incomplete is null ? observed : $"{observed}; the primary reports incomplete results from {result.Incomplete}";

    static string Token(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    static SendOptions RouteHere(string runId)
    {
        var send = new SendOptions();
        send.RouteToThisEndpoint();
        send.SetHeader(SmokeHeaders.RunId, runId);
        return send;
    }

    Task SendSmoke(SmokeContext ctx, object message, string? messageId = null, Action<SendOptions>? configure = null, string? destination = null, CancellationToken ct = default)
    {
        var send = RouteHere(ctx.RunId);
        if (destination is not null)
        {
            send = new SendOptions();
            send.SetDestination(destination);
            send.SetHeader(SmokeHeaders.RunId, ctx.RunId);
        }

        if (messageId is not null)
        {
            send.SetMessageId(messageId);
        }

        configure?.Invoke(send);
        return session.Send(message, send, ct);
    }
}
