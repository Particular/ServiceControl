namespace ServiceControl.Audit.Auditing
{
    public interface IEnrichImportedAuditMessages
    {
        void Enrich(AuditEnricherContext context);
    }
}