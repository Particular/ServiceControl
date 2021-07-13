namespace ServiceControl.Operations
{
    interface IEnrichImportedErrorMessages
    {
        void Enrich(ErrorEnricherContext context);
    }
}