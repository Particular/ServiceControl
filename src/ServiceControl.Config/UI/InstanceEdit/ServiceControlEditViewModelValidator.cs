namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using ServiceControl.Config.UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlEditViewModelValidator : AbstractValidator<ServiceControlEditViewModel>
    {
        public ServiceControlEditViewModelValidator()
        {
            RuleFor(x => x.ServiceControl).SetValidator(new ServiceControlInformationEditValidator());
            

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();
            
            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

        
        }

    }
}