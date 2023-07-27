namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    abstract class TestPersistence
    {
        public abstract void Configure(IServiceCollection services);

        public virtual Task CompleteDatabaseOperation() => Task.CompletedTask;

        public virtual Task CleanupDatabase() => Task.CompletedTask;

        public override string ToString() => GetType().Name;
    }
}