namespace ServiceControl.Auditing
{
    interface IEnrichImportedAuditMessages
    {
        void Enrich(AuditEnricherContext context);
    }
}
