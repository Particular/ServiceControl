namespace ServiceControl.Config.UI.Upgrades
{
    using System.Windows.Input;
    using Framework.Rx;
    using InstanceAdd;
    using Validar;
    using Validation;

    [InjectValidation]
    public class AddNewAuditInstanceViewModel : RxScreen
    {
        public AddNewAuditInstanceViewModel(string serviceControlName)
        {
            ServiceControlAudit = new ServiceControlAuditInformation();
            ServiceControlAudit.ApplyConventionalServiceName(serviceControlName);
        }

        public ServiceControlAuditInformation ServiceControlAudit { get; set; }
        public ValidationTemplate ValidationTemplate { get; set; }
        public ICommand Cancel { get; set; }
        public ICommand Continue { get; set; }
        public bool SubmitAttempted { get; set; }
    }
}