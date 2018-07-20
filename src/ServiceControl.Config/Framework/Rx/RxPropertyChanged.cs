namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using Caliburn.Micro;
    using PropertyChanging;
    using ReactiveUI;

    public class RxPropertyChanged : ReactiveObject, INotifyPropertyChangedEx
    {
        [DoNotNotify]
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