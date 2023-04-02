namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using System;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlEditViewModelValidator : AbstractValidator<ServiceControlEditViewModel>
    {
        public ServiceControlEditViewModelValidator()
        {
            RuleFor(x => x.ServiceControl.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ServiceControl.ErrorForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING);

            RuleFor(x => x.ServiceControl.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error","Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorQueueName != "!disable");

            RuleFor(x => x.ServiceControl.ErrorForwardingQueueName)
                .NotEmpty().WithMessage(string.Format(Validation.Validations.MSG_FORWARDINGQUEUENAME, "Error Forwarding"))
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error Forwarding", "Error"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.PortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControl.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Database Maintenance"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControl.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Port"))
                .NotEqual(x => x.ServiceControl.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "ServiceControl"))
                .When(x => x.SubmitAttempted);
        }
    }
}