﻿namespace ServiceControl.Config.UI.MessageBox
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using FluentValidation;
    using Framework;
    using Framework.Rx;
    using Validar;
    using Validation;

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
            Cancel = Command.Create(async () =>
            {
                Result = null;
                await this.DeactivateAsync(true);
            });
            Save = Command.Create(async () =>
            {
                Result = true;
                await this.DeactivateAsync(true);
            }, () => Validator.Validate(new ValidationContext<TextBoxDialogViewModel>(this)).IsValid);
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