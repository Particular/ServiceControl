using System.Runtime.CompilerServices;
using ServiceControl.CompositeViews.Endpoints;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Contracts.EndpointControl;
using ServiceControl.Contracts.EventLog;
using ServiceControl.Contracts.HeartbeatMonitoring;
using ServiceControl.Contracts.MessageFailures;
using ServiceControl.Contracts.Operations;
using ServiceControl.CustomChecks;
using ServiceControl.EndpointControl.Contracts;
using ServiceControl.EndpointControl.InternalMessages;
using ServiceControl.EndpointPlugin.Messages.SagaState;
using ServiceControl.ExternalIntegrations;

//in the FailedMessage's metadata
using ServiceControl.HeartbeatMonitoring.InternalMessages;
using ServiceControl.MessageFailures.InternalMessages;

[assembly: TypeForwardedTo(typeof(EndpointDetails))]

//in ExternalIntegrationDispatchRequest document
[assembly: TypeForwardedTo(typeof(CustomCheckFailedPublisher))]
[assembly: TypeForwardedTo(typeof(CustomCheckSucceededPublisher))]
[assembly: TypeForwardedTo(typeof(HeartbeatRestoredPublisher))]
[assembly: TypeForwardedTo(typeof(HeartbeatStoppedPublisher))]
[assembly: TypeForwardedTo(typeof(MessageFailedPublisher))]

//Saga plugin
[assembly: TypeForwardedTo(typeof(SagaUpdatedMessage))]

[assembly: TypeForwardedTo(typeof(DisableEndpointMonitoring))]
[assembly: TypeForwardedTo(typeof(EnableEndpointMonitoring))]
[assembly: TypeForwardedTo(typeof(CustomChecksUpdated))]
[assembly: TypeForwardedTo(typeof(CustomCheckDeleted))]
[assembly: TypeForwardedTo(typeof(DeleteCustomCheck))]
[assembly: TypeForwardedTo(typeof(RegisterEndpoint))]
[assembly: TypeForwardedTo(typeof(RegisterPotentiallyMissingHeartbeats))]
[assembly: TypeForwardedTo(typeof(ArchiveMessage))]
[assembly: TypeForwardedTo(typeof(ImportFailedMessage))]
[assembly: TypeForwardedTo(typeof(PerformRetry))]
[assembly: TypeForwardedTo(typeof(RegisterSuccessfulRetry))]
[assembly: TypeForwardedTo(typeof(RequestRetryAll))]
[assembly: TypeForwardedTo(typeof(RetryMessage))]
[assembly: TypeForwardedTo(typeof(CustomCheckFailed))]
[assembly: TypeForwardedTo(typeof(CustomCheckSucceeded))]
[assembly: TypeForwardedTo(typeof(EndpointStarted))]
[assembly: TypeForwardedTo(typeof(MonitoringDisabledForEndpoint))]
[assembly: TypeForwardedTo(typeof(MonitoringEnabledForEndpoint))]
[assembly: TypeForwardedTo(typeof(NewEndpointDetected))]
[assembly: TypeForwardedTo(typeof(EventLogItemAdded))]
[assembly: TypeForwardedTo(typeof(EndpointFailedToHeartbeat))]
[assembly: TypeForwardedTo(typeof(EndpointHeartbeatRestored))]
[assembly: TypeForwardedTo(typeof(HeartbeatingEndpointDetected))]
[assembly: TypeForwardedTo(typeof(HeartbeatMonitoringDisabled))]
[assembly: TypeForwardedTo(typeof(HeartbeatMonitoringEnabled))]
[assembly: TypeForwardedTo(typeof(HeartbeatStatusChanged))]
[assembly: TypeForwardedTo(typeof(HeartbeatsUpdated))]
[assembly: TypeForwardedTo(typeof(FailedMessageArchived))]
[assembly: TypeForwardedTo(typeof(MessageFailed))]
[assembly: TypeForwardedTo(typeof(MessageFailedRepeatedly))]
[assembly: TypeForwardedTo(typeof(MessageFailureResolved))]
[assembly: TypeForwardedTo(typeof(MessageFailureResolvedByRetry))]
[assembly: TypeForwardedTo(typeof(MessageFailuresUpdated))]
[assembly: TypeForwardedTo(typeof(MessageSubmittedForRetry))]
