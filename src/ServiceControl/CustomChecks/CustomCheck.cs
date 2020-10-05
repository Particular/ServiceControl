namespace ServiceControl.CustomChecks
{
    using System;
    using Contracts.Operations;

    public class CustomCheck
    {
        public static string MakeDocumentId(Guid customCheckId)
        {
            return $"CustomChecks/{customCheckId}";
        }

        public string Id { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string FailureReason { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
    }
}