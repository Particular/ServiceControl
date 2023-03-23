namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using FluentValidation;
    using FluentValidation.Results;
    using System.Linq;
    using ServiceControl.Config.Framework.Rx;
    using PropertyChanged;

    public class FooViewModel : RxScreen, INotifyDataErrorInfo, IDataErrorInfo
    {
        public FooViewModel()
        {
            //PropertyChanged += Validate;
        }

        ValidationResult _validationResult = new ValidationResult();

        public string this[string columnName]
        {
            get
            {
                var strings = _validationResult.Errors
                    .Where(x => x.PropertyName == columnName)
                    .Select(x => x.ErrorMessage);

                return string.Join(Environment.NewLine, strings);
            }
        }

        public string Bar { get; set; }

        public string Bar2 { get; set; }

        public bool Saving { get; set; }

        [DoNotNotify]
        public bool HasErrors => !_validationResult.IsValid;

        public string Error
        {
            get
            {
                var strings = _validationResult.Errors
                    .Select(x => x.ErrorMessage)
                    .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool CanSave()
        {
            return !HasErrors;
        }

        public IEnumerable GetErrors(string propertyName) =>
             _validationResult.Errors
                .Where(x => x.PropertyName == propertyName)
                .Select(x => x.ErrorMessage);

        public void Save()
        {
            Saving = true;
            Validate();
            Saving = false;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            Validate();
        }

        public void Validate()
        {
            _validationResult = null;

            var validator = new FooViewModelValidator();

            _validationResult = validator.Validate(this);

            foreach (var error in _validationResult.Errors)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(error.PropertyName));
            }

        }
    }

    public class FooViewModelValidator : AbstractValidator<FooViewModel>
    {
        public FooViewModelValidator()
        {
            RuleFor(vm => vm.Bar)
                .NotEmpty()
                .WithMessage("A bar is required when working on ServiceControl.");

            RuleFor(vm => vm.Bar2)
                .NotEmpty()
                .WithMessage("A second bar is required when working on ServiceControl.");
        }
    }
}
