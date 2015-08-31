namespace ServiceControl.Config.Framework.Rx
{
    public class RxProgressScreen : RxScreen, IProgressViewModel
    {
        public string ProgressTitle { get; set; }

        public bool InProgress { get; set; }

        public string ProgressMessage { get; set; }

        public int ProgressPercent { get; set; }

        public bool ProgressIndeterminate { get { return ProgressPercent < 0; } }
    }
}