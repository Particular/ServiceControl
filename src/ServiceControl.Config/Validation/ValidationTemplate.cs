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
    using Extensions;
    using FluentValidation;
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
            properties = new HashSet<string>(target.GetType().GetProperties().Select(p => p.Name));
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
            var validationResult = validator.Validate(target);
            validationResults.AddRange(validationResult.Errors);
            RaiseErrorsChanged();
            return validationResults.Count == 0;
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
            if (e.PropertyName == "Error" || e.PropertyName == "HasErrors")
            {
                return;
            }

            if (properties.Contains(e.PropertyName))
            {
                validationResults.RemoveAll(x => x.PropertyName == e.PropertyName);
                var validationResult = validator.Validate(target, e.PropertyName);
                validationResults.AddRange(validationResult.Errors);
            }
            else
            {
                validationResults.Clear();
                var validationResult = validator.Validate(target);
                validationResults.AddRange(validationResult.Errors);
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
        List<ValidationFailure> validationResults = new List<ValidationFailure>();
        Subject<DataErrorsChangedEventArgs> errorsChangedSubject;
        HashSet<string> properties;
        static ConcurrentDictionary<RuntimeTypeHandle, IValidator> validators = new ConcurrentDictionary<RuntimeTypeHandle, IValidator>();
    }
}