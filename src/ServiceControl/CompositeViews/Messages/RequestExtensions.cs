namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using Nancy;

    public static class RequestExtensions
    {
        public static Uri RedirectToRemoteUri(this Request request, string remoteUri)
        {
            var returnVal = $"{remoteUri}{request.Path}";

            if (!string.IsNullOrWhiteSpace(request.Url.Query))
            {
                returnVal += $"?{request.Url.Query}";
            }

            return new Uri(returnVal);
        }
    }
}