namespace ServiceControl.Persistence;

using System.Threading.Tasks;

public interface IConnectionStringProvider
{
    public Task<string> GetConnectionString();
}