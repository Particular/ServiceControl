using ServiceControl.Config.Framework.Rx;

namespace ServiceControl.Config.UI.NoInstances
{
    using System.Windows.Input;
    using ServiceControl.Config.Commands;

    class NoInstancesViewModel : RxScreen
    {
        public NoInstancesViewModel(AddServiceControlInstanceCommand addInstance)
        {
            DisplayName = "DEPLOYED INSTANCES";

            AddInstance = addInstance;
        }

        public ICommand AddInstance { get; }

        [FeatureToggle(Feature.MonitoringInstances)]
        public bool ShowMonitoringInstances { get; set; }
    }
}