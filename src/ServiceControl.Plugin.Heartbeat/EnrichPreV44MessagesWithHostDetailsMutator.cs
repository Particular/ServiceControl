namespace ServiceControl.Plugin.Heartbeat
{
    using EndpointPlugin.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.MessageMutator;

    class EnrichPreV44MessagesWithHostDetailsMutator:IMutateIncomingTransportMessages,INeedInitialization
    {
        public void MutateIncoming(TransportMessage transportMessage)
        {
            if (transportMessage.Headers.ContainsKey(Headers.HostId))
            {
                return;
            }

            transportMessage.Headers[Headers.HostId] = HostInformation.HostId.ToString("N");
            transportMessage.Headers[Headers.HostDisplayName] = HostInformation.DisplayName;
        }

        HostInformation HostInformation
        {
            get
            {
                if (cachedHostInformation == null)
                {
                    cachedHostInformation = HostInformationRetriever.RetrieveHostInfo();
                }

                return cachedHostInformation;
            }
        }

        static HostInformation cachedHostInformation;

        public void Init()
        {
            //no need for this in v4.4 and above
            if (VersionChecker.CoreVersionIsAtLeast(4, 4))
            {
                return;
            }

            Configure.Component<EnrichPreV44MessagesWithHostDetailsMutator>(DependencyLifecycle.SingleInstance);    
        }
    }
}