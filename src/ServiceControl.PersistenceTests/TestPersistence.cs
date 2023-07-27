namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    abstract class TestPersistence
    {
        public abstract Task Configure(IServiceCollection services);

        public virtual Task CompleteDatabaseOperation() => Task.CompletedTask;

        public virtual Task CleanupDatabase() => Task.CompletedTask;
    }
}