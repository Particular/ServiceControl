namespace ServiceControl.Config.UI.MessageBox
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using Framework;
    using Framework.Rx;

    class YesNoCancelViewModel : RxScreen
    {
        public YesNoCancelViewModel(string title, string message, string question, string yesText, string noText)
        {
            Title = title;
            Message = message;
            YesText = yesText;
            NoText = noText;
            Question = question;
            Cancel = Command.Create(() =>
            {
                Result = null;
                ((IDeactivate)this).Deactivate(true);
            });
            No = Command.Create(() =>
            {
                Result = false;
                ((IDeactivate)this).Deactivate(true);
            });
            Yes = Command.Create(() =>
            {
                Result = true;
                ((IDeactivate)this).Deactivate(true);
            });
            ShowCancelButton = true;
        }

        public string Title { get; set; }
        public string Message { get; set; }
        public string Question { get; set; }
        public ICommand Cancel { get; }
        public ICommand No { get; }
        public string NoText { get; }
        public ICommand Yes { get; }
        public string YesText { get; }
        public bool ShowCancelButton { get; set; }
    }
}