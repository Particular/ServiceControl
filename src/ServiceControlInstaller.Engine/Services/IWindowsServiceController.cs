namespace ServiceControlInstaller.Engine.Services
{
    using System;
    using System.ServiceProcess;

    public interface IWindowsServiceController
    {
        string ServiceName { get; }
        string ExePath { get; }
        string Description { get; set; }
        ServiceControllerStatus Status { get; }
        string Account { get; }
        string DisplayName { get; }

        void Refresh();
        void WaitForStatus(ServiceControllerStatus stopped, TimeSpan timeSpan);
        void Start();
        void Stop();
        void SetStartupMode(string v);
        void Delete();
        void ChangeAccountDetails(string accountName, string serviceAccountPwd);
    }
}