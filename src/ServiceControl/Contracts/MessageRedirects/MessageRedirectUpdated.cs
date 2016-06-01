namespace ServiceControl.Contracts.MessageRedirects
{
    using System;

    public class MessageRedirectUpdated
    {
        public string MessageRedirectId { get; set; }
        public string MatchMessageType { get; set; }
        public string MatchSourceEndpoint { get; set; }
        public string RedirectToEndpoint { get; set; }
        public DateTime AsOfDateTime { get; set; }
        public DateTime ExpiresDateTime { get; set; }
    }
}