namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence;
    using Recoverability.ExternalIntegration;
    using ServiceBus.Management.Infrastructure.Settings;

    /// <summary>
    /// Maps ServiceControl integration events to Alertmanager alerts. Firing and resolving alerts for the same
    /// problem always carry identical labels, so receivers can correlate them.
    /// </summary>
    class AlertmanagerAlertFactory(IFailedMessageQueryDataStore failedMessageStore, Settings settings, TimeProvider timeProvider)
    {
        public async Task<IReadOnlyList<AlertmanagerAlert>> CreateAlerts(IReadOnlyCollection<object> events, CancellationToken cancellationToken = default)
        {
            var failedMessages = await LoadFailedMessages(events, cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var alerts = new List<AlertmanagerAlert>();

            foreach (var @event in events)
            {
                switch (@event)
                {
                    case Contracts.MessageFailed messageFailed when messageFailed.Status != Contracts.MessageFailed.MessageStatus.ArchivedFailure:
                        alerts.Add(FromMessageFailed(messageFailed, endsAt: null));
                        break;
                    case Contracts.MessageFailureResolvedByRetry resolvedByRetry:
                        AddResolved(alerts, failedMessages, [resolvedByRetry.FailedMessageId, .. resolvedByRetry.AlternativeFailedMessageIds ?? []], now);
                        break;
                    case Contracts.MessageFailureResolvedManually resolvedManually:
                        AddResolved(alerts, failedMessages, [resolvedManually.FailedMessageId], now);
                        break;
                    case Contracts.MessageEditedAndRetried editedAndRetried:
                        AddResolved(alerts, failedMessages, [editedAndRetried.FailedMessageId], now);
                        break;
                    case Contracts.FailedMessagesArchived archived:
                        AddResolved(alerts, failedMessages, archived.FailedMessagesIds ?? [], now);
                        break;
                    case Contracts.FailedMessagesUnArchived unarchived:
                        AddRefired(alerts, failedMessages, unarchived.FailedMessagesIds ?? []);
                        break;
                    case Contracts.HeartbeatStopped heartbeatStopped:
                        alerts.Add(FromHeartbeatStopped(heartbeatStopped));
                        break;
                    case Contracts.HeartbeatRestored heartbeatRestored:
                        alerts.Add(FromHeartbeatRestored(heartbeatRestored));
                        break;
                    case Contracts.CustomCheckFailed customCheckFailed:
                        alerts.Add(FromCustomCheckFailed(customCheckFailed));
                        break;
                    case Contracts.CustomCheckSucceeded customCheckSucceeded:
                        alerts.Add(FromCustomCheckSucceeded(customCheckSucceeded));
                        break;
                    default:
                        break;
                }
            }

            return KeepLatestPerAlert(alerts);
        }

        // Resolution events only carry IDs and can be stale by the time they are dispatched (e.g. a message that was
        // resolved and has failed again since), so the current state of each failed message decides the outcome.
        void AddResolved(List<AlertmanagerAlert> alerts, Dictionary<Guid, FailedMessage> failedMessages, IEnumerable<string> ids, DateTime now)
        {
            foreach (var failedMessage in Lookup(failedMessages, ids))
            {
                if (failedMessage.Status is FailedMessageStatus.Resolved or FailedMessageStatus.Archived)
                {
                    alerts.Add(FromMessageFailed(failedMessage.ToEvent(), endsAt: now));
                }
            }
        }

        void AddRefired(List<AlertmanagerAlert> alerts, Dictionary<Guid, FailedMessage> failedMessages, IEnumerable<string> ids)
        {
            foreach (var failedMessage in Lookup(failedMessages, ids))
            {
                if (failedMessage.Status == FailedMessageStatus.Unresolved)
                {
                    alerts.Add(FromMessageFailed(failedMessage.ToEvent(), endsAt: null));
                }
            }
        }

        static IEnumerable<FailedMessage> Lookup(Dictionary<Guid, FailedMessage> failedMessages, IEnumerable<string> ids) =>
            ids.Select(id => Guid.TryParse(id, out var guid) && failedMessages.TryGetValue(guid, out var failedMessage) ? failedMessage : null)
                .Where(failedMessage => failedMessage != null);

        async Task<Dictionary<Guid, FailedMessage>> LoadFailedMessages(IEnumerable<object> events, CancellationToken cancellationToken)
        {
            var ids = events
                .SelectMany(@event => @event switch
                {
                    Contracts.MessageFailureResolvedByRetry e => [e.FailedMessageId, .. e.AlternativeFailedMessageIds ?? []],
                    Contracts.MessageFailureResolvedManually e => [e.FailedMessageId],
                    Contracts.MessageEditedAndRetried e => [e.FailedMessageId],
                    Contracts.FailedMessagesArchived e => e.FailedMessagesIds ?? [],
                    Contracts.FailedMessagesUnArchived e => e.FailedMessagesIds ?? [],
                    _ => Enumerable.Empty<string>()
                })
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            var result = new Dictionary<Guid, FailedMessage>();
            foreach (var chunk in ids.Chunk(LookupBatchSize))
            {
                foreach (var failedMessage in await failedMessageStore.GetFailedMessagesByIds(chunk, cancellationToken))
                {
                    if (Guid.TryParse(failedMessage.UniqueMessageId, out var id))
                    {
                        result[id] = failedMessage;
                    }
                }
            }

            return result;
        }

        AlertmanagerAlert FromMessageFailed(Contracts.MessageFailed message, DateTime? endsAt)
        {
            var endpoint = message.ProcessingEndpoint?.Name;
            var exception = message.FailureDetails?.Exception;
            var link = ServicePulseLink($"failed-messages/message/{message.FailedMessageId}");

            var labels = Labels("MessageFailed", "error");
            Add(labels, AlertLabels.Endpoint, endpoint);
            Add(labels, AlertLabels.MessageType, message.MessageType);
            Add(labels, AlertLabels.FailedMessageId, message.FailedMessageId);

            var annotations = Annotations(
                labels,
                endsAt.HasValue
                    ? $"Failed message '{message.MessageType ?? message.FailedMessageId}' in endpoint '{endpoint}' has been resolved"
                    : $"Failed to process message '{message.MessageType ?? message.FailedMessageId}' in endpoint '{endpoint}'",
                exception?.ExceptionType == null ? null : Truncate($"{exception.ExceptionType}: {exception.Message}"),
                link);
            Add(annotations, AlertAnnotations.ExceptionType, exception?.ExceptionType);
            Add(annotations, AlertAnnotations.ExceptionMessage, Truncate(exception?.Message));
            Add(annotations, AlertAnnotations.FailingAddress, message.FailureDetails?.AddressOfFailingEndpoint);
            Add(annotations, AlertAnnotations.Host, message.ProcessingEndpoint?.Host);
            Add(annotations, AlertAnnotations.SendingEndpoint, message.SendingEndpoint?.Name);
            Add(annotations, AlertAnnotations.MessageId, message.MessageDetails?.MessageId);
            Add(annotations, AlertAnnotations.ProcessingAttempts, message.NumberOfProcessingAttempts.ToString(CultureInfo.InvariantCulture));

            return new AlertmanagerAlert
            {
                Labels = labels,
                Annotations = annotations,
                StartsAt = Timestamp(message.FailureDetails?.TimeOfFailure),
                EndsAt = Timestamp(endsAt),
                GeneratorUrl = link
            };
        }

        AlertmanagerAlert FromHeartbeatStopped(Contracts.HeartbeatStopped heartbeat)
        {
            var labels = HeartbeatLabels(heartbeat.EndpointName, heartbeat.Host, $"{heartbeat.HostId}");
            var link = HeartbeatLink(heartbeat.EndpointName);
            var lastReceivedAt = AlertmanagerPayload.ToUtc(heartbeat.LastReceivedAt);

            var annotations = Annotations(
                labels,
                $"Endpoint '{heartbeat.EndpointName}' on host '{heartbeat.Host}' stopped sending heartbeats",
                $"No heartbeat has been received from endpoint '{heartbeat.EndpointName}' on host '{heartbeat.Host}' since {lastReceivedAt:O}.",
                link);
            Add(annotations, AlertAnnotations.LastHeartbeatAt, lastReceivedAt.ToString("O", CultureInfo.InvariantCulture));

            return new AlertmanagerAlert
            {
                Labels = labels,
                Annotations = annotations,
                StartsAt = Timestamp(heartbeat.DetectedAt),
                GeneratorUrl = link
            };
        }

        AlertmanagerAlert FromHeartbeatRestored(Contracts.HeartbeatRestored heartbeat)
        {
            var labels = HeartbeatLabels(heartbeat.EndpointName, heartbeat.Host, $"{heartbeat.HostId}");
            var link = HeartbeatLink(heartbeat.EndpointName);

            return new AlertmanagerAlert
            {
                Labels = labels,
                Annotations = Annotations(
                    labels,
                    $"Endpoint '{heartbeat.EndpointName}' on host '{heartbeat.Host}' is sending heartbeats again",
                    null,
                    link),
                EndsAt = Timestamp(heartbeat.RestoredAt),
                GeneratorUrl = link
            };
        }

        AlertmanagerAlert FromCustomCheckFailed(Contracts.CustomCheckFailed check)
        {
            var labels = CustomCheckLabels(check.EndpointName, check.Host, $"{check.HostId}", check.CustomCheckId, check.Category);
            var link = ServicePulseLink("custom-checks");

            var annotations = Annotations(
                labels,
                $"Custom check '{check.CustomCheckId}' failed on endpoint '{check.EndpointName}'",
                Truncate(check.FailureReason),
                link);
            Add(annotations, AlertAnnotations.FailureReason, Truncate(check.FailureReason));

            return new AlertmanagerAlert
            {
                Labels = labels,
                Annotations = annotations,
                StartsAt = Timestamp(check.FailedAt),
                GeneratorUrl = link
            };
        }

        AlertmanagerAlert FromCustomCheckSucceeded(Contracts.CustomCheckSucceeded check)
        {
            var labels = CustomCheckLabels(check.EndpointName, check.Host, $"{check.HostId}", check.CustomCheckId, check.Category);
            var link = ServicePulseLink("custom-checks");

            return new AlertmanagerAlert
            {
                Labels = labels,
                Annotations = Annotations(labels, $"Custom check '{check.CustomCheckId}' succeeded on endpoint '{check.EndpointName}'", null, link),
                EndsAt = Timestamp(check.SucceededAt),
                GeneratorUrl = link
            };
        }

        Dictionary<string, string> HeartbeatLabels(string endpoint, string host, string hostId)
        {
            var labels = Labels("HeartbeatStopped", "critical");
            Add(labels, AlertLabels.Endpoint, endpoint);
            Add(labels, AlertLabels.Host, host);
            Add(labels, AlertLabels.HostId, hostId);
            return labels;
        }

        Dictionary<string, string> CustomCheckLabels(string endpoint, string host, string hostId, string customCheckId, string category)
        {
            var labels = Labels("CustomCheckFailed", "warning");
            Add(labels, AlertLabels.Endpoint, endpoint);
            Add(labels, AlertLabels.Host, host);
            Add(labels, AlertLabels.HostId, hostId);
            Add(labels, AlertLabels.CustomCheckId, customCheckId);
            Add(labels, AlertLabels.Category, category);
            return labels;
        }

        Dictionary<string, string> Labels(string alertName, string severity) => new()
        {
            [AlertLabels.AlertName] = alertName,
            [AlertLabels.Severity] = severity,
            [AlertLabels.Instance] = settings.InstanceName
        };

        static Dictionary<string, string> Annotations(Dictionary<string, string> labels, string summary, string description, string link)
        {
            var annotations = new Dictionary<string, string>
            {
                [AlertAnnotations.Summary] = summary,
                [AlertAnnotations.DedupKey] = AlertmanagerAlert.ComputeDedupKey(labels)
            };
            Add(annotations, AlertAnnotations.Description, description);
            Add(annotations, AlertAnnotations.ServicePulseUrl, link);
            return annotations;
        }

        string HeartbeatLink(string endpointName) => ServicePulseLink($"heartbeats/instances/{Uri.EscapeDataString(endpointName ?? string.Empty)}");

        string ServicePulseLink(string route) => string.IsNullOrEmpty(settings.ServicePulseUrl) ? null : $"{settings.ServicePulseUrl}/#/{route}";

        static void Add(Dictionary<string, string> values, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        static DateTime? Timestamp(DateTime? value) => value is null || value == default(DateTime) ? null : AlertmanagerPayload.ToUtc(value.Value);

        static string Truncate(string value) => value == null || value.Length <= MaxAnnotationLength ? value : string.Concat(value.AsSpan(0, MaxAnnotationLength - 1), "…");

        // When the same alert changes state several times within a batch, only its latest state is relevant
        static List<AlertmanagerAlert> KeepLatestPerAlert(List<AlertmanagerAlert> alerts)
        {
            var latest = new Dictionary<string, AlertmanagerAlert>();
            var order = new List<string>();
            foreach (var alert in alerts)
            {
                if (latest.ContainsKey(alert.Fingerprint))
                {
                    order.Remove(alert.Fingerprint);
                }

                latest[alert.Fingerprint] = alert;
                order.Add(alert.Fingerprint);
            }

            return [.. order.Select(fingerprint => latest[fingerprint])];
        }

        const int LookupBatchSize = 256;
        const int MaxAnnotationLength = 1000;
    }
}
