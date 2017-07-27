using System;
using Caliburn.Micro;
using ReactiveUI;

namespace ServiceControl.Config.Framework.Rx
{
    public class RxPropertyChanged : ReactiveObject, INotifyPropertyChangedEx
    {
        [PropertyChanging.DoNotNotify]
        [PropertyChanged.DoNotNotify]
        [Obsolete("Use SuppressChangeNotifications() instead.", true)]
        bool INotifyPropertyChangedEx.IsNotifying
        {
            get { throw new NotSupportedException("Use SuppressChangeNotifications() instead."); }
            set { throw new NotSupportedException("Use SuppressChangeNotifications() instead."); }
        }

        public void NotifyOfPropertyChange(string propertyName)
        {
            raisePropertyChanged(propertyName);
        }

        void INotifyPropertyChangedEx.Refresh()
        {
            raisePropertyChanged(string.Empty);
        }
    }
}