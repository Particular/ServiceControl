namespace ServiceControl.Config.UI.FeedBack
{
    using System.Windows.Input;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Framework.Rx;

    public class FeedBackNoticeViewModel : RxScreen
    {
        public ICommand OpenUrl { get; private set; }

        public FeedBackNoticeViewModel()
        {
            OpenUrl = new OpenURLCommand();
        }
    }
}
