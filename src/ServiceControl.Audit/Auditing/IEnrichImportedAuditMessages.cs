namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;

    interface IEnrichImportedAuditMessages
    {
        void Enrich(AuditEnricherContext context);
    }
}