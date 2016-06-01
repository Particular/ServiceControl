namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;

    public class CreateMessageRedirect : IHaveMessageRedirectData
    {
        public Guid MessageRedirectId { get; set; }
        public string MatchMessageType { get; set; }
        public string MatchSourceEndpoint { get; set; }
        public string RedirectToEndpoint { get; set; }
        public DateTime AsOfDateTime { get; set; }
        public DateTime ExpiresDateTime { get; set; }
    }
}