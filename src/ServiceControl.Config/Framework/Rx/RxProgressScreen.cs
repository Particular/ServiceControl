namespace ServiceControl.Config.Framework.Rx
{
    using System;

    public class RxProgressScreen : RxScreen, IProgressViewModel
    {
        public bool ProgressIndeterminate => ProgressPercent < 0;

        public string ProgressTitle
        {
            get { return _progressTitle; }
            set
            {
                _progressTitle = value;
                NotifyUpdates();
            }
        }

        public bool InProgress
        {
            get { return _inProgress; }
            set
            {
                _inProgress = value;
                NotifyUpdates();
            }
        }

        public string ProgressMessage
        {
            get { return _progressMessage; }
            set
            {
                _progressMessage = value;
                NotifyUpdates();
            }
        }

        public int ProgressPercent
        {
            get { return _progressPercent; }
            set
            {
                _progressPercent = value;
                NotifyUpdates();
            }
        }

        public override void CanClose(Action<bool> callback)
        {
            callback(!InProgress);
        }

        void NotifyUpdates()
        {
            NotifyOfPropertyChange("ProgressTitle");
            NotifyOfPropertyChange("InProgress");
            NotifyOfPropertyChange("ProgressMessage");
            NotifyOfPropertyChange("ProgressPercent");
        }

        string _progressTitle;
        int _progressPercent;
        string _progressMessage;
        bool _inProgress;
    }
}