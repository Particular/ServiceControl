namespace ServiceControl.Config.UI.FeedBack
{
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.Framework.Rx;

    class FeedBackResultViewModel : RxScreen
    {
        public FeedBackResultViewModel(bool showSuccess = false)
        {
            OK = Command.Create(() => TryClose(false));
        }

        public string Title { get; private set; }
        public string Message { get; private set; }

        public ICommand OK { get; private set; }

        public void SetResult(bool showSuccess)
        {
            Title = (showSuccess) ? "FEEDBACK SENT SUCCESSFULLY" : "SEND FEEDBACK FAILED";
            Message = (showSuccess) ? "Thanks for sharing your feedback" : "Failed to send feedback";
        }
    }
}