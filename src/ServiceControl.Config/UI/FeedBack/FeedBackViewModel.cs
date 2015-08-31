namespace ServiceControl.Config.UI.FeedBack
{
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.Framework.Rx;

    class FeedBackViewModel : RxScreen
    {
        RaygunFeedback feedBack;

        public FeedBackViewModel(RaygunFeedback raygunFeedBack)
        {
            feedBack = raygunFeedBack;
            Cancel = Command.Create(() => TryClose(false));
            SendFeedBack = Command.Create(() => Send());
        }

        public string EmailAddress { get; set; }
        public string Message { get; set; }
        public bool IncludeSystemInfo { get; set; }

        public bool Success { get; private set; }

        public ICommand Cancel { get; set; }
        public ICommand SendFeedBack { get; set; }

        void Send()
        {
            try
            {
                feedBack.SendFeedBack(EmailAddress, Message, IncludeSystemInfo);
                Success = true;
            }
            catch
            {
                Success = false;
            }
            TryClose(true);
        }
    }
}