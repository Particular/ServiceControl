namespace ServiceControl.RavenDB
{
    using System.Collections.Generic;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Session;

    public static class RavenClientCompatibility
    {
        public static void UseOptimisticConcurrencyForWrites(this IAdvancedDocumentSessionOperations advanced)
        {
#if RAVENDB6
            advanced.UseOptimisticConcurrency = true;
#else
            advanced.OptimisticConcurrencyMode = OptimisticConcurrencyMode.Writes;
#endif
        }

        public static SingleNodeBatchCommand CreateBatchCommand(this IAdvancedDocumentSessionOperations advanced, DocumentConventions conventions, IList<ICommandData> commands) =>
#if RAVENDB6
            new(conventions, advanced.Context, commands);
#else
            new(conventions, commands);
#endif
    }
}
