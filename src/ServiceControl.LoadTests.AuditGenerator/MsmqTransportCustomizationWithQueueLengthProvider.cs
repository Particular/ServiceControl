namespace ServiceControl.LoadTests.AuditGenerator
{
    using ServiceControl.Transports;
    using ServiceControl.Transports.Msmq;

    public class MsmqTransportCustomizationWithQueueLengthProvider : MsmqTransportCustomization
    {
        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new MsmqQueueLengthProvider();
        }
    }
}