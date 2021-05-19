namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Net.Http;

    public static class RequestExtensions
    {
        public static Uri RedirectToRemoteUri(this HttpRequestMessage request, Uri remoteUri)
        {
            return new Uri($"{remoteUri.GetLeftPart(UriPartial.Authority)}{request.RequestUri.PathAndQuery}");
        }
    }
}