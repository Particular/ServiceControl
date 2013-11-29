namespace ServiceControl.Plugin.DebugSession
{
    using NServiceBus;
    using NServiceBus.MessageMutator;

    class ApplyDebugSessionId:IMutateOutgoingTransportMessages
    {
        public string SessionId { get; set; }
        public void MutateOutgoing(object[] messages, TransportMessage transportMessage)
        {
            transportMessage.Headers["ServiceControl.DebugSessionId"] = SessionId;
        }
    }
}