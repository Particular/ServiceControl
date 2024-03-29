﻿namespace ServiceControl.Config.Framework.Rx
{
    using System.Threading;
    using System.Threading.Tasks;

    public class RxProgressScreen : RxScreen, IProgressViewModel
    {
        public bool ProgressIndeterminate => ProgressPercent < 0;

        public string ProgressTitle
        {
            get => progressTitle;
            set
            {
                progressTitle = value;
                NotifyUpdates();
            }
        }

        public bool InProgress
        {
            get => inProgress;
            set
            {
                inProgress = value;
                NotifyUpdates();
            }
        }

        public string ProgressMessage
        {
            get => progressMessage;
            set
            {
                progressMessage = value;
                NotifyUpdates();
            }
        }

        public int ProgressPercent
        {
            get => progressPercent;
            set
            {
                progressPercent = value;
                NotifyUpdates();
            }
        }

        public override Task<bool> CanCloseAsync(CancellationToken cancellationToken) => Task.FromResult(!InProgress);

        void NotifyUpdates()
        {
            NotifyOfPropertyChange("ProgressTitle");
            NotifyOfPropertyChange("InProgress");
            NotifyOfPropertyChange("ProgressMessage");
            NotifyOfPropertyChange("ProgressPercent");
        }

        string progressTitle;
        int progressPercent;
        string progressMessage;
        bool inProgress;
    }
}