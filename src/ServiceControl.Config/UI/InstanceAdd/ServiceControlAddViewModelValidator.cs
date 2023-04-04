namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;
    public class ServiceControlAddViewModelValidator : AbstractValidator<ServiceControlAddViewModel>
    {
        public ServiceControlAddViewModelValidator()
        {
            RuleFor(x => x.ConventionName)
                .NotEmpty()
                .When(x =>
                        x.SubmitAttempted &&
                       ((x.InstallAuditInstance
                         && string.IsNullOrWhiteSpace(x.AuditInstanceName))
                        ||
                        (x.InstallErrorInstance
                         && string.IsNullOrWhiteSpace(x.ErrorInstanceName))));

            RuleFor(x => x.SelectedTransport)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.InstallAuditInstance)
                .Must(install => install)
                    .WithMessage("Must select either an audit or an error instance.")
                .When(x => !x.InstallErrorInstance);

            RuleFor(x => x.InstallErrorInstance)
                .Must(install => install)
                    .WithMessage("Must select either an audit or an error instance.")
                .When(x => !x.InstallAuditInstance);

            RuleFor(x => x.ConnectionString)
                .NotEmpty()
                    .WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(viewModel => viewModel.ErrorInstanceName)
                .NotEmpty()
                .When(viewModel => viewModel.InstallErrorInstance);

            RuleFor(x => x.ErrorServiceAccount)
                .NotEmpty()
                .When(x => x.InstallErrorInstance);

            RuleFor(viewModel => viewModel.ErrorHostName)
                .NotEmpty()
                .When(viewModel => viewModel.InstallErrorInstance);

            RuleFor(x => x.ErrorPortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Port"))
                .PortAvailable() //across windows
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.ErrorInstanceName)) //across all instances
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .NotEqual(x => x.ErrorDatabaseMaintenancePortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Database Maintenance"))
                .NotEqual(x => x.AuditPortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Audit"))
                .NotEqual(x => x.AuditDatabaseMaintenancePortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Audit Database Maintenance"))
                .When(x => x.InstallErrorInstance);

            RuleFor(x => x.ErrorDatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Database Maintenance Port"))
                .PortAvailable() //across windows
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.ErrorInstanceName)) //across all instances
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Port"))
                .NotEqual(x => x.ErrorPortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "ServiceControl"))
                .NotEqual(x => x.AuditPortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit"))
                .NotEqual(x => x.AuditDatabaseMaintenancePortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit Database Maintenance"))
                .When(x => x.InstallErrorInstance);

            RuleFor(x => x.ErrorDestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ErrorInstanceName))
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Error Destination Paths"))
                .When(x => x.InstallErrorInstance);

            RuleFor(x => x.ErrorLogPath)
                .NotEmpty()
                .ValidPath()
                .When(x => x.InstallErrorInstance);

            RuleFor(x => x.ErrorDatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ErrorInstanceName))
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .When(x => x.InstallErrorInstance);

            RuleFor(x => x.ErrorForwarding)
                .NotNull()
                    .WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING)
                .When(x => x.InstallErrorInstance);


            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error", "Error Forwarding"))
                .NotEqual(x => x.AuditQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.AuditForwardingQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ErrorInstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ErrorInstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .When(x => x.InstallErrorInstance && x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error Forwarding", "Error"))
                .NotEqual(x => x.AuditQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.AuditForwardingQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ErrorInstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ErrorInstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .When(x => x.InstallErrorInstance && x.ErrorForwarding.Value);
        }

    }
}