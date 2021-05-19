namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Net.Http;

    static class RequestExtensions
    {
        public static Uri RedirectToRemoteUri(this HttpRequestMessage request, Uri remoteUri)
        {
            return new Uri($"{remoteUri.GetLeftPart(UriPartial.Authority)}{request.RequestUri.PathAndQuery}");
        }
    }
}