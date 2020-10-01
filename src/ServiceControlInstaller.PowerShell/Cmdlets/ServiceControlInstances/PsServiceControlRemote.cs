namespace ServiceControlInstaller.PowerShell
{
    using Engine.Configuration.ServiceControl;

    public class PsServiceControlRemote
    {
        public string ApiUrl { get; set; }

        public static PsServiceControlRemote FromRemote(RemoteInstanceSetting remoteInstance)
            => new PsServiceControlRemote
            {
                ApiUrl = remoteInstance.ApiUri
            };
    }
}