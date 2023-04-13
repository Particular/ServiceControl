namespace ServiceControl.Config.UI.InstanceAdd;

using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Framework.Rx;
using PropertyChanged;
using ServiceControlInstaller.Engine.Instances;
using Validation;

public class ServiceControlEditorViewModel : RxProgressScreen
{
    public ServiceControlEditorViewModel()
    {
        Transports = ServiceControlCoreTransports.All.Where(t => t.AvailableInSCMU);
        ServiceControl = new ServiceControlInformation(this);
        ServiceControlAudit = new ServiceControlAuditInformation(this);
    }

    [DoNotNotify]
    public ValidationTemplate ValidationTemplate { get; set; }

    public IEnumerable<TransportInfo> Transports { get; }

    public TransportInfo SelectedTransport
    {
        get => selectedTransport;
        set
        {
            ConnectionString = null;
            selectedTransport = value;
        }
    }

    public bool OneInstanceTypeSelected => InstallErrorInstance || InstallAuditInstance;

    public string TransportWarning => SelectedTransport?.Help;

    public string ConnectionString { get; set; }

    public string SampleConnectionString => SelectedTransport?.SampleConnectionString;

    public bool ShowConnectionString => !string.IsNullOrEmpty(SelectedTransport?.SampleConnectionString);

    public ServiceControlInformation ServiceControl { get; set; }

    public ServiceControlAuditInformation ServiceControlAudit { get; set; }

    public bool InstallErrorInstance { get; set; } = true;
    public bool InstallAuditInstance { get; set; } = true;

    public ICommand Cancel { get; set; }

    public ICommand Save { get; set; }

    TransportInfo selectedTransport;
}