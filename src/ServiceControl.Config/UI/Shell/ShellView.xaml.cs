using Caliburn.Micro;
using ServiceControl.Config.Events;

namespace ServiceControl.Config.UI.Shell
{
    partial class ShellView
    {
        public ShellView()
        {
            InitializeComponent();

            Activated += (s, e) => IoC.Get<IEventAggregator>().PublishOnUIThread(new RefreshInstances());
        }
    }
}