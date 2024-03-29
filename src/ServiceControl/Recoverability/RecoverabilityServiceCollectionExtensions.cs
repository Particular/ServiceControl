namespace ServiceControl.Recoverability
{
    using Microsoft.Extensions.DependencyInjection;
    using Operations;

    static class RecoverabilityServiceCollectionExtensions
    {
        public static void AddErrorMessageEnricher<T>(this IServiceCollection serviceCollection)
            where T : class, IEnrichImportedErrorMessages
        {
            serviceCollection.AddSingleton<IEnrichImportedErrorMessages, T>();
        }
    }
}