namespace ServiceControl.Config.Framework.Rx
{
    using System;

    public class RxProgressScreen : RxScreen, IProgressViewModel
    {
        public string ProgressTitle { get; set; }

        public bool InProgress { get; set; }

        public string ProgressMessage { get; set; }

        public int ProgressPercent { get; set; }

        public bool ProgressIndeterminate => ProgressPercent < 0;

        public override void CanClose(Action<bool> callback)
        {
            callback(!InProgress);
        }
    }
}