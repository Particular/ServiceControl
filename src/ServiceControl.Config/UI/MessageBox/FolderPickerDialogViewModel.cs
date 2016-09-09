// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace ServiceControl.Config.UI.MessageBox
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using Validar;

    [InjectValidation]
    public class FolderPickerDialogViewModel : RxScreen
    {
        ValidationTemplate validationTemplate;

        public FolderPickerDialogViewModel()
        {
            validationTemplate = new ValidationTemplate(this);
        }
        
        public string Title { get; set;  }
        public string PathHeader { get; set; }
        public string Path { get; set; }
        public string Message { get; set; }
        public string Question { get;  set; }
        public string YesButtonText { get;  set; }
        public string NoButtonText { get; set; }
        public bool HideNoButton { get; set; }
        public bool SubmitAttempted { get; set; }
        public bool ValidateFolderIsEmpty { get; set; }
        void Submit()
        {
            SubmitAttempted = true;
            if (!validationTemplate.Validate())
            {
                NotifyOfPropertyChange(string.Empty);
                SubmitAttempted = false;
                return;
            }
            Result = true;
            ((IDeactivate)this).Deactivate(true);
        }
        public ICommand YesCommand => Command.Create(() => Submit());
        public ICommand NoCommand =>  Command.Create(() => { Result = false; ((IDeactivate)this).Deactivate(true); });
        public ICommand CancelCommand => Command.Create(() => { Result = null; ((IDeactivate)this).Deactivate(true); });
        public ICommand SelectPath => new SelectPathCommand(p => Path = p, isFolderPicker: true);
    }
}
