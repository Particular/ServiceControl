namespace ServiceControl.Config.UI.MessageBox
{
    using System;
    using System.Windows.Input;
    using Caliburn.Micro;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;

    public class SliderDialogViewModel : RxScreen
    {
        public SliderDialogViewModel(string title, string message, string saveText, string periodHeader, string periodExplanation, TimeSpan period)
        {
            Title = title;
            Message = message;
            SaveText = saveText;
            Period = period;
            PeriodHeader = periodHeader;
            PeriodExplanation = periodExplanation;
            Cancel = Command.Create(() => { Result = null; ((IDeactivate)this).Deactivate(true); });
            Save = Command.Create(() => { Result = true; ((IDeactivate)this).Deactivate(true); });

        }

        public string PeriodHeader { get; set; }
        public string PeriodExplanation { get; set; }
        public TimeSpan Period { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public ICommand Cancel { get; private set; }
        public ICommand Save { get; private set; }
        public string SaveText { get; private set; }
    }
}