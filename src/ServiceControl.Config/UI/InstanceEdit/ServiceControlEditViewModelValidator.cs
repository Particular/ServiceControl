namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using Validation;

    public class ServiceControlEditViewModelValidator : SharedServiceControlEditorViewModelValidator<ServiceControlEditViewModel>
    {
        public ServiceControlEditViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.AuditForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.ErrorForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTERRORFORWARDING);

            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ErrorForwarding.Value);

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.AuditQueueName != "!disable");

            RuleFor(x => x.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.AuditForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => UsedPorts(x.InstanceName))
                .NotEqual(x => x.PortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.DatabaseMaintenancePortNumberRequired && x.SubmitAttempted);
        }
    }
}