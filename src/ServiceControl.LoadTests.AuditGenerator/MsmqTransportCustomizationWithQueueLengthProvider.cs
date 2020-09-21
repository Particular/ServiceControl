using ServiceControl.Transports;
using ServiceControl.Transports.Msmq;

namespace ServiceControl.LoadTests.AuditGenerator
{
    public class MsmqTransportCustomizationWithQueueLengthProvider : MsmqTransportCustomization
    {
        public override IProvideQueueLength CreateQueueLengthProvider()
        {
            return new MsmqQueueLengthProvider();
        }
    }
}