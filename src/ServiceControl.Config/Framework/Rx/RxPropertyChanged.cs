namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using Caliburn.Micro;
    using ReactiveUI;
    using PropertyChangingDoNotNotify = PropertyChanging.DoNotNotifyAttribute;
    using PropertyChangedDoNotNotify = PropertyChanged.DoNotNotifyAttribute;

    public class RxPropertyChanged : ReactiveObject, INotifyPropertyChangedEx
    {
        [PropertyChangingDoNotNotify]
        [PropertyChangedDoNotNotify]
        bool INotifyPropertyChangedEx.IsNotifying
        {
            get { throw new NotSupportedException("Use SuppressChangeNotifications() instead."); }
            set { throw new NotSupportedException("Use SuppressChangeNotifications() instead."); }
        }

        public void NotifyOfPropertyChange(string propertyName) => this.RaisePropertyChanged(propertyName);

        void INotifyPropertyChangedEx.Refresh() => this.RaisePropertyChanged(string.Empty);
    }
}