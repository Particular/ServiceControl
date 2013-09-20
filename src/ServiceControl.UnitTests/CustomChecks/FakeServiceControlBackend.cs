namespace ServiceControl.UnitTests
{
    using System.Collections.Generic;
    using EndpointPlugin.Messages.Operations.ServiceControlBackend;
   
    public class FakeServiceControlBackend : IServiceControlBackend
    {
        //public List<ReportCustomCheckResult> ReportedChecks = new List<ReportCustomCheckResult>();

    //    public void Send(ReportCustomCheckResult reportCustomCheck)
      //  {
        //    ReportedChecks.Add(reportCustomCheck);
        //}

        public List<object> MessagesSent = new List<object>();
 
        public void Send(object messageToSend)
        {
            MessagesSent.Add(messageToSend);
        }

        public void Send(object messageToSend, System.TimeSpan timeToBeReceived)
        {
            MessagesSent.Add(messageToSend);
        }

        public NServiceBus.Address Address
        {
            get { return null; }
        }
    }
}