namespace ServiceControl.Persistence
{
    /// <summary>
    /// The keys of the failed message summary. They are the JSON property names the API returns, so
    /// every persister has to produce the same ones.
    /// </summary>
    public static class FailedMessageSummaryKeys
    {
        public const string Endpoints = "Endpoints";
        public const string Hosts = "Hosts";
        public const string MessageTypes = "Message types";
    }
}
