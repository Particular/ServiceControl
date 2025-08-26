namespace ServiceControl.Audit.Persistence.Postgresql
{
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Persistence;

    public class PostgresqlPersistence : IPersistence
    {
        public void AddInstaller(IServiceCollection services) => throw new System.NotImplementedException();
        public void AddPersistence(IServiceCollection services) => throw new System.NotImplementedException();
    }
}
