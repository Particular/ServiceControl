namespace ServiceControl.Connection
{
    using System.Threading.Tasks;

    public interface IPlatformConnectionBuilder
    {
        Task<PlatformConnectionDetails> BuildPlatformConnection();
    }
}