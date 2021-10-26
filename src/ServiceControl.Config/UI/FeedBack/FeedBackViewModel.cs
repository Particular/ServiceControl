namespace ServiceControl.Config.UI.FeedBack
{
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Framework;
    using Framework.Rx;
    using Validar;
    using Validation;

    [InjectValidation]
    public class FeedBackViewModel : RxScreen
    {
        public FeedBackViewModel(RaygunFeedback raygunFeedBack)
        {
            feedBack = raygunFeedBack;
            validationTemplate = new ValidationTemplate(this);
            Cancel = Command.Create(async () => await TryCloseAsync(false));
            SendFeedBack = Command.Create(async () => await Send());
        }

        public string EmailAddress { get; set; }

        public string Message { get; set; }
        public bool IncludeSystemInfo { get; set; }

        public bool Success { get; private set; }

        public ICommand Cancel { get; set; }
        public ICommand SendFeedBack { get; set; }

        public bool SubmitAttempted { get; set; }

        async Task Send()
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

            await TryCloseAsync(true);
        }

        RaygunFeedback feedBack;
        ValidationTemplate validationTemplate;
    }
}