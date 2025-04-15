namespace ServiceControl.Config.Validation
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using FluentValidation;
    using FluentValidation.Internal;
    using FluentValidation.Results;
    using Framework.Rx;

    public class ValidationTemplate : IDataErrorInfo, INotifyDataErrorInfo
    {
        public ValidationTemplate(RxPropertyChanged target)
        {
            this.target = target;
            if (target is IProvideValidator providesValidator)
            {
                validator = providesValidator.Validator;
            }
            else
            {
                validator = GetValidator(target.GetType());
            }

            errorsChangedSubject = new Subject<DataErrorsChangedEventArgs>();
            target.PropertyChanged += Validate;
            properties = [.. target.GetType().GetProperties().Select(p => p.Name)];
        }

        public IObservable<DataErrorsChangedEventArgs> ErrorsChangedObservable => errorsChangedSubject.AsObservable();

        public string Error
        {
            get
            {
                var strings = validationResults
                    .Select(x => x.ErrorMessage)
                    .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public string this[string propertyName]
        {
            get
            {
                var strings = validationResults
                    .Where(x => x.PropertyName == propertyName)
                    .Select(x => x.ErrorMessage)
                    .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            return validationResults
                .Where(x => x.PropertyName == propertyName)
                .Select(x => x.ErrorMessage);
        }

        public bool HasErrors => validationResults.Any();

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool Validate()
        {
            validationResults.Clear();

            var errors = Validate(target).Distinct(ValidationFailureEqualityComparer.Instance);

            validationResults.AddRange(errors);

            RaiseErrorsChanged();
            return validationResults.Count == 0;
        }

        IEnumerable<ValidationFailure> Validate(RxPropertyChanged target)
        {
            var validationResult = GetValidator(target.GetType()).Validate(new ValidationContext<RxPropertyChanged>(target));
            IEnumerable<ValidationFailure> errors = validationResult.Errors;

            var childPropertiesWithValidators = target.GetType().GetProperties()
                .Where(prop => validators.ContainsKey(prop.PropertyType.TypeHandle))
                .Select(prop => (RxPropertyChanged)prop.GetValue(target));

            foreach (var childTarget in childPropertiesWithValidators)
            {
                var childErrors = Validate(childTarget);
                errors = errors.Union(childErrors);
            }

            return errors;
        }

        static IValidator GetValidator(Type modelType)
        {
            if (!validators.TryGetValue(modelType.TypeHandle, out var validator))
            {
                var typeName = $"{modelType.Namespace}.{modelType.Name}Validator";
                var type = modelType.Assembly.GetType(typeName, true);
                validators[modelType.TypeHandle] = validator = (IValidator)Activator.CreateInstance(type);
            }

            return validator;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Error" or "HasErrors")
            {
                return;
            }

            if (properties.Contains(e.PropertyName))
            {
                validationResults.RemoveAll(x => x.PropertyName == e.PropertyName);
                var validationContext = new ValidationContext<RxPropertyChanged>(
                    target,
                    new PropertyChain(),
                    ValidatorOptions.Global.ValidatorSelectors.MemberNameValidatorSelectorFactory(new[] { e.PropertyName })
                );
                var validationResult = validator.Validate(validationContext);
                validationResults.AddRange(validationResult.Errors.Distinct(ValidationFailureEqualityComparer.Instance));
            }
            else
            {
                validationResults.Clear();
                Validate();
            }

            RaiseErrorsChanged();
        }

        void RaiseErrorsChanged()
        {
            var hashSet = new HashSet<string>(validationResults.Select(x => x.PropertyName));
            foreach (var error in hashSet)
            {
                RaiseErrorsChanged(error);
            }

            if (!hashSet.Any())
            {
                RaiseErrorsChanged(null);
            }

            // Manually trigger other property updates.
            target.NotifyOfPropertyChange("Error");
            target.NotifyOfPropertyChange("HasErrors");
        }

        void RaiseErrorsChanged(string propertyName)
        {
            var dataErrorsChangedEventArgs = new DataErrorsChangedEventArgs(propertyName);

            var handler = ErrorsChanged;
            handler?.Invoke(this, dataErrorsChangedEventArgs);

            errorsChangedSubject.OnNext(dataErrorsChangedEventArgs);
        }

        RxPropertyChanged target;
        IValidator validator;
        List<ValidationFailure> validationResults = [];
        Subject<DataErrorsChangedEventArgs> errorsChangedSubject;
        HashSet<string> properties;
        static ConcurrentDictionary<RuntimeTypeHandle, IValidator> validators = new ConcurrentDictionary<RuntimeTypeHandle, IValidator>();
    }
}