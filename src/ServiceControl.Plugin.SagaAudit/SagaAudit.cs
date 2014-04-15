namespace ServiceControl.Features
{
    using NServiceBus.Features;

    public class SagaAudit:Feature
    {
        public override void Initialize()
        {
            
        }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override bool ShouldBeEnabled()
        {
            return IsEnabled<Sagas>();
        }
    }
}