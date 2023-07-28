namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    abstract class TestPersistence
    {
        public abstract void Configure(IServiceCollection services);

        public abstract Task CompleteDatabaseOperation();

        public override string ToString() => GetType().Name;
    }
}