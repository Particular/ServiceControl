namespace ServiceBus.Management.Extensions
{
    using System.Globalization;
    using Nancy;
    using Nancy.Responses.Negotiation;
    using Raven.Client;

    public static class NegotiatorExtensions
    {
        public static Negotiator WithTotalCount(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            return negotiator.WithHeader("Total-Count", stats.TotalResults.ToString(CultureInfo.InvariantCulture));
        }
    }
}