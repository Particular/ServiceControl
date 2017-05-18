namespace ServiceControl.Config.Validation
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Instances;
    using UI.SharedInstanceEditor;

    public class SharedMonitoringEditorViewModelValidator<T> : AbstractValidator<T> where T : SharedMonitoringEditorViewModel
    {
        ReadOnlyCollection<MonitoringInstance> MonitoringInstances;

        // We need this to ignore the instance that represents the edit screen
        protected List<string> UsedPaths(string instanceName = null)
        {
            return MonitoringInstances
               .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
               .SelectMany(p => new[]
               {
                    p.LogPath,
                    p.InstallPath
               })
               .Distinct()
               .ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        protected List<string> UsedPorts(string instanceName = null)
        {
            return MonitoringInstances
               .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
               .Select(p => p.Port.ToString())
               .Distinct()
               .ToList();
        }

        protected SharedMonitoringEditorViewModelValidator()
        {
            MonitoringInstances = InstanceFinder.MonitoringInstances();

            RuleFor(x => x.InstanceName)
                .NotEmpty()
                .MustNotContainWhitespace()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.HostName)
                .NotEmpty().When(x => x.SubmitAttempted);

           

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);
        }
    }
}