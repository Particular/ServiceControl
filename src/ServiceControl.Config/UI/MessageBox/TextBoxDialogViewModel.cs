namespace ServiceControl.Config.UI.MessageBox
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using FluentValidation;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using Validar;

    [InjectValidation]
    public class TextBoxDialogViewModel : RxScreen, IProvideValidator
    {
        public TextBoxDialogViewModel(string title,
                                    string message,
                                    string header,
                                    string currentValue,
                                    IValidator validator)
        {
            Title = title;
            Message = message;
            Value = currentValue;
            Validator = validator;
            Header = header;
            Cancel = Command.Create(() =>{Result = null;((IDeactivate)this).Deactivate(true);});
            Save = Command.Create(() =>{Result = true; ((IDeactivate)this).Deactivate(true);}, () => Validator.Validate(this).IsValid);
        }

        public string Header { get; set; }

        public string Value { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public ICommand Cancel { get; }
        public ICommand Save { get; }
        public IValidator Validator { get; }
    }
}