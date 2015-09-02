namespace ServiceControl.Config.UI.FeedBack
{
    using PropertyChanged;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using Validar;

    [InjectValidation]
    public class FeedBackViewModel : RxScreen
    {
        RaygunFeedback feedBack;

        public FeedBackViewModel(RaygunFeedback raygunFeedBack)
        {
            feedBack = raygunFeedBack;
            Cancel = Command.Create(() => TryClose(false));
            SendFeedBack = Command.Create(() => Send());
        }

        [DoNotNotify]
        public ValidationTemplate ValidationTemplate { get; set; }

        public string EmailAddress { get; set; }
        public string Message { get; set; }
        public bool IncludeSystemInfo { get; set; }

        public bool Success { get; private set; }

        public ICommand Cancel { get; set; }
        public ICommand SendFeedBack { get; set; }

        void Send()
        {

            if (!ValidationTemplate.Validate())
            {
                NotifyOfPropertyChange(string.Empty);
                return;
            }

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