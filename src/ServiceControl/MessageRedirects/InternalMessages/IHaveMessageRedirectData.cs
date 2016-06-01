namespace ServiceControl.MessageRedirects.InternalMessages
{
    using System;

    public interface IHaveMessageRedirectData
    {
        Guid MessageRedirectId { get; set; }
        string MatchMessageType { get; set; }
        string MatchSourceEndpoint { get; set; }
        string RedirectToEndpoint { get; set; }
        DateTime AsOfDateTime { get; set; }
        DateTime ExpiresDateTime { get; set; }
    }
}