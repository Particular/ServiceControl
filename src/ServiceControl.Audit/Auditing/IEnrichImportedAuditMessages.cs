namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;

    interface IEnrichImportedAuditMessages
    {
        Task Enrich(AuditEnricherContext context);
    }
}