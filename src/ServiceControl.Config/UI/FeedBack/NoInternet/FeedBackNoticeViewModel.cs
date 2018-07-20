namespace ServiceControl.Config.UI.FeedBack
{
    using System.Windows.Input;
    using Commands;
    using Framework.Rx;

    public class FeedBackNoticeViewModel : RxScreen
    {
        public FeedBackNoticeViewModel()
        {
            OpenUrl = new OpenURLCommand();
        }

        public ICommand OpenUrl { get; }
    }
}