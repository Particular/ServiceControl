using System.Windows.Input;
using ServiceControl.Config.Commands;
using ServiceControl.Config.Framework.Rx;

namespace ServiceControl.Config.UI.NoInstances
{
    class NoInstancesViewModel : RxScreen
    {
        public NoInstancesViewModel(AddInstanceCommand addInstance)
        {
            DisplayName = "DEPLOYED INSTANCES";

            AddInstance = addInstance;
        }

        public ICommand AddInstance { get; }
    }
}