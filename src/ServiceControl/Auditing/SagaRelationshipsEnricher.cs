namespace ServiceControl.Auditing
{
    using ServiceControl.SagaAudit;

    class SagaRelationshipsEnricher : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context) => InvokedSagasParser.Parse(context.Headers, context.Metadata);
    }
}
