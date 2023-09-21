namespace ServiceControl.PersistenceTests
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    abstract class TestPersistence
    {
        public abstract void Configure(IServiceCollection services);

        public abstract void CompleteDatabaseOperation();

        public override string ToString() => GetType().Name;

        [Conditional("DEBUG")]
        public abstract void BlockToInspectDatabase();

        public abstract Task TearDown();
    }
}