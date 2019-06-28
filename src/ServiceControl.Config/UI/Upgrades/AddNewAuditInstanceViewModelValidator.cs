namespace ServiceControl.Config.UI.Upgrades
{
    using FluentValidation;
    using Extensions;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;
    public class AddNewAuditInstanceViewModelValidator : AbstractValidator<AddNewAuditInstanceViewModel>
    {
        public AddNewAuditInstanceViewModelValidator()
        {
            var serviceControlInstances = InstanceFinder.ServiceControlInstances();
            var serviceControlAuditInstances = InstanceFinder.ServiceControlAuditInstances();

            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => serviceControlInstances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .MustNotBeIn(x => serviceControlAuditInstances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => serviceControlInstances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .MustNotBeIn(x => serviceControlAuditInstances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => serviceControlInstances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .MustNotBeIn(x => serviceControlAuditInstances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => serviceControlInstances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .MustNotBeIn(x => serviceControlAuditInstances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.ServiceControlAudit.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .When(x => x.SubmitAttempted && (x.ServiceControlAudit.AuditForwarding?.Value ?? false));
        }
    }
}