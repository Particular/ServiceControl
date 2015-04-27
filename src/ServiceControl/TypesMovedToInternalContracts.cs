using System.Runtime.CompilerServices;
using ServiceControl.Contracts.Operations;
using ServiceControl.ExternalIntegrations;

//in the FailedMessage's metadata
[assembly: TypeForwardedTo(typeof(EndpointDetails))]

//in ExternalIntegrationDispatchRequest document
[assembly: TypeForwardedTo(typeof(CustomCheckFailedPublisher.DispatchContext))]
[assembly: TypeForwardedTo(typeof(CustomCheckSucceededPublisher.DispatchContext))]
[assembly: TypeForwardedTo(typeof(HeartbeatRestoredPublisher.DispatchContext))]
[assembly: TypeForwardedTo(typeof(HeartbeatStoppedPublisher.DispatchContext))]
[assembly: TypeForwardedTo(typeof(MessageFailedPublisher.DispatchContext))]
