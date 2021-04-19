namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    interface IEnrichImportedErrorMessages
    {
        void Enrich(ErrorEnricherContext context);
    }
}