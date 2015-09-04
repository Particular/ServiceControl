namespace ServiceControl.Config.UI.FeedBack
{
    using System.Reactive.Linq;
    using System.Windows.Input;
    using ReactiveUI;
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
            SendFeedBack = new ReactiveCommand(validationTemplate.ErrorsChangedObservable.Select(_ => !validationTemplate.HasErrors).DistinctUntilChanged())
                .DoAction(_ => Send());
        }

        public string EmailAddress { get; set; }

        public string Message { get; set; }
        public bool IncludeSystemInfo { get; set; }

        public bool Success { get; private set; }

        public ICommand Cancel { get; set; }
        public ICommand SendFeedBack { get; set; }

        void Send()
        {
            if (!validationTemplate.Validate())
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