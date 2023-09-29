namespace ServiceControl.PersistenceTests
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Persistence;

    abstract class TestPersistence
    {
        public PersistenceSettings Settings { get; protected set; }

        public abstract void Configure(IServiceCollection services);

        public abstract void CompleteDatabaseOperation();

        public override string ToString() => GetType().Name;

        [Conditional("DEBUG")]
        public abstract void BlockToInspectDatabase();

        public abstract Task TearDown();
    }
}