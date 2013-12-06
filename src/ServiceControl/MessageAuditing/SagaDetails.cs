namespace ServiceControl.MessageAuditing
{
    using System.Collections.Generic;
    using NServiceBus;

    public class SagaDetails
    {
        public SagaDetails()
        {
        }

        public SagaDetails(IDictionary<string, string> headers)
        {
            SagaId = headers[Headers.SagaId];
            SagaType = headers[Headers.SagaType];
            IsTimeoutMessage = headers.ContainsKey(Headers.IsSagaTimeoutMessage);
        }


        protected bool IsTimeoutMessage { get; set; }

        public string SagaId { get; set; }

        public string SagaType { get; set; }

        public static SagaDetails Parse(IDictionary<string,string> headers)
        {
            return !headers.ContainsKey(Headers.SagaId) ? null : new SagaDetails(headers);
        }
    }
}