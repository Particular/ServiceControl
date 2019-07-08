namespace ServiceControl.Config.UI.Upgrades
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class AddNewAuditInstanceViewModelValidator : AbstractValidator<AddNewAuditInstanceViewModel>
    {
        public AddNewAuditInstanceViewModelValidator()
        {
            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(Validation.Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .WithMessage(Validation.Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validation.Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validation.Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.ServiceControlAudit.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .When(x => x.SubmitAttempted && (x.ServiceControlAudit.AuditForwarding?.Value ?? false));
        }
    }
}