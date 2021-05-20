namespace ServiceControl.EndpointControl.Handlers
{
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using ServiceControl.Contracts.Operations;

    public class ExtractSenderAndReceiverDetailsFromErrors : Feature
    {
        public ExtractSenderAndReceiverDetailsFromErrors()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectNewEndpointsFromErrorImportsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class DetectNewEndpointsFromErrorImportsEnricher : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(context.Headers);

                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                if (sendingEndpoint != null)
                {
                    context.Metadata.Add("SendingEndpoint", sendingEndpoint);
                }

                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(context.Headers);
                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (receivingEndpoint != null)
                {
                    context.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
                }
            }
        }
    }
}