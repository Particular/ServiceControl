namespace ServiceControl.Api
{
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;

    public interface IConfigurationApi
    {
        public RootUrls GetUrls(string baseUrl);
        public object GetConfig();
        public Task<object> GetRemoteConfigs();
    }
}
