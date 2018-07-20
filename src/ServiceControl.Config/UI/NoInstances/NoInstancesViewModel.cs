namespace ServiceControl.Config.UI.NoInstances
{
    using System.Windows.Input;
    using Commands;
    using Framework.Rx;

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