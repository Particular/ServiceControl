namespace ServiceControl.Audit.SagaAudit
{
    using Auditing;
    using ServiceControl.SagaAudit;

    class SagaRelationshipsEnricher : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;
            var metadata = context.Metadata;

            InvokedSagasParser.Parse(headers, metadata);
        }
    }
}