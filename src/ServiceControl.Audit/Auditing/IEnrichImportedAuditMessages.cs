namespace ServiceControl.Audit.Auditing
{
    interface IEnrichImportedAuditMessages
    {
        void Enrich(AuditEnricherContext context);
    }
}