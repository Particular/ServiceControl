namespace ServiceControl.Api.Contracts
{
    public class RootUrls
    {
        public string Description { get; set; }
        public string EndpointsErrorUrl { get; set; }
        public string KnownEndpointsUrl { get; set; }
        public string EndpointsMessageSearchUrl { get; set; }
        public string EndpointsMessagesUrl { get; set; }
        public string AuditCountUrl { get; set; }
        public string EndpointsUrl { get; set; }
        public string ErrorsUrl { get; set; }
        public string Configuration { get; set; }
        public string RemoteConfiguration { get; set; }
        public string MessageSearchUrl { get; set; }
        public string LicenseStatus { get; set; }
        public string LicenseDetails { get; set; }
        public string Name { get; set; }
        public string SagasUrl { get; set; }
        public string EventLogItems { get; set; }
        public string ArchivedGroupsUrl { get; set; }
        public string GetArchiveGroup { get; set; }
    }
}