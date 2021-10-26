namespace ServiceControl.Config.UI.FeedBack
{
    using Framework;
    using Framework.Rx;

    class FeedBackResultViewModel : RxScreen
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public FeedBackResultViewModel(bool showSuccess = false)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            OK = Command.Create(async () => await TryCloseAsync(false));
        }

        public string Title { get; private set; }
        public string Message { get; private set; }

        public System.Windows.Input.ICommand OK { get; }

        public void SetResult(bool showSuccess)
        {
            Title = showSuccess ? "FEEDBACK SENT SUCCESSFULLY" : "SEND FEEDBACK FAILED";
            Message = showSuccess ? "Thanks for sharing your feedback" : "Failed to send feedback";
        }
    }
}