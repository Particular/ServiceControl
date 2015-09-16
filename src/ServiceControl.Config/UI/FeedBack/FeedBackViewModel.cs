namespace ServiceControl.Config.UI.FeedBack
{
    using System.Windows.Input;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using Validar;

    [InjectValidation]
    public class FeedBackViewModel : RxScreen
    {
        RaygunFeedback feedBack;
        ValidationTemplate validationTemplate;

        public FeedBackViewModel(RaygunFeedback raygunFeedBack)
        {
            feedBack = raygunFeedBack;
            validationTemplate = new ValidationTemplate(this);
            Cancel = Command.Create(() => TryClose(false));
            SendFeedBack = Command.Create(() => Send());
        }

        public string EmailAddress { get; set; }

        public string Message { get; set; }
        public bool IncludeSystemInfo { get; set; }

        public bool Success { get; private set; }

        public ICommand Cancel { get; set; }
        public ICommand SendFeedBack { get; set; }

        public bool SubmitAttempted { get; set; }
        
        void Send()
        {
            SubmitAttempted = true;
            if (!validationTemplate.Validate())
            {
                NotifyOfPropertyChange(string.Empty);
                SubmitAttempted = false;
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