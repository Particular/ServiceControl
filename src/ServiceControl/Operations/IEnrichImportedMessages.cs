namespace ServiceControl.Operations
{
    public interface IEnrichImportedErrorMessages
    {
        void Enrich(ErrorEnricherContext context);
    }
}