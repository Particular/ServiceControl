namespace ServiceControl.Persistence.UnitOfWork
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // HINT: This allows an implementor to provide only part of the implementation and allow the other part
    // to be handled by an existing implementation. This way a new persistence does not need to cover both
    // recoverability and monitoring. It can focus on one at a time.
    class FallbackIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly IIngestionUnitOfWork primary;
        readonly IIngestionUnitOfWork fallback;

        public FallbackIngestionUnitOfWork(IIngestionUnitOfWork primary, IIngestionUnitOfWork fallback)
        {
            this.primary = primary ?? throw new ArgumentNullException(nameof(primary));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            Monitoring = primary.Monitoring
                         ?? fallback.Monitoring
                         ?? throw new InvalidOperationException("Fallback unit of work must implement Monitoring");
            Recoverability = primary.Recoverability
                             ?? fallback.Recoverability
                             ?? throw new InvalidOperationException("Fallback unit of work must implement Recoverability");
        }

        public override Task Complete(CancellationToken cancellationToken = default)
            => Task.WhenAll(
                primary.Complete(cancellationToken),
                fallback.Complete(cancellationToken)
            );

        protected override async ValueTask DisposeAsyncCore()
        {
            await primary.DisposeAsync();
            await fallback.DisposeAsync();
        }
    }
}