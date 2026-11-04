# Google Cloud Pub/Sub transport

The primary, audit, and monitoring instances can run on the `NServiceBus.Transport.GooglePubSub` package. The transport maps each queue to a topic and a subscription with the same ID. This page covers how ServiceControl uses the transport and which platform features the transport limits.

## Connection string

The connection string accepts `ProjectId`, `ResourceNamePrefix`, and `ServiceEndpoint`, and each maps to the `GooglePubSubTransportOptions` property of the same name. All three are optional. The parser rejects any other key, so credentials cannot end up in the connection string or in the instance configuration.

Without `ProjectId`, the transport reads the `GOOGLE_CLOUD_PROJECT` or `GCLOUD_PROJECT` environment variable, then the Google Cloud metadata server. When `PUBSUB_EMULATOR_HOST` is set, the transport connects to the Pub/Sub emulator.

## Credentials

ServiceControl uses Application Default Credentials (ADC) for every component. Error ingestion, audit ingestion, retries, and the throughput queue create their receivers through `TransportCustomization.CreateTransportInfrastructure`, which passes no service provider to the transport. Without a service provider, the transport uses ADC. A credential registered in dependency injection would reach only the NServiceBus endpoints.

## Transaction mode

The transport supports `None` and `ReceiveOnly`. The retry dequeuer requests `SendsAtomicWithReceive` and falls back to `ReceiveOnly`. Forwarding a retried message and acknowledging its staged copy are separate operations, so a failure between them makes ServiceControl forward the message again.

## Delayed delivery

The transport throws for delayed sends and saga timeouts, and it limits delayed retries to 600 seconds. ServiceControl sends no delayed messages and disables sagas. The primary and monitoring instances set delayed retries to zero, which passes the transport's startup check. The audit instance is send-only, so the transport skips the check for it.

## Time to be received

The transport does not support time to be received, and its dispatcher ignores the setting. Heartbeats, custom check results, and metrics that endpoints send to ServiceControl stay in their subscription until an instance reads them or the subscription's message retention expires. The transport creates subscriptions with seven days of retention.

## Integration events

The primary publishes integration events to topics named `{ResourceNamePrefix}events.{full type name}`. The transport creates an event topic only when an endpoint subscribes to the event with installers enabled. Until then, each publish logs a warning and Pub/Sub has nowhere to deliver the event, so no subscriber misses it. ServiceControl does not create these topics itself, because that would duplicate the transport's naming rules.

## Queue length

The monitoring instance does not report queue lengths for this transport yet. The Pub/Sub API has no backlog count, so queue length has to come from Cloud Monitoring.

## Headers with empty values

The transport drops headers whose value is empty or null, because Pub/Sub does not deliver an attribute with an empty value. ServiceControl's own headers always have a value. A header that a user empties while editing a failed message is not sent with the retried message.
