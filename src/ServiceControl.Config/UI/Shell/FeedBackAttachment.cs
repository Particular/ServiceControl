namespace ServiceControl.Config.UI.Shell
{
    using System;
    using FeedBack;
    using Framework;

    class FeedBackAttachment : Attachment<ShellViewModel>
    {
        public FeedBackAttachment(
            RaygunFeedback raygunFeedBack,
            IWindowManagerEx windowManager,
            Func<FeedBackViewModel> feedBackFactory,
            Func<FeedBackResultViewModel> feedBackResultFactory,
            Func<FeedBackNoticeViewModel> feedBackNoticeFactory)
        {
            this.feedBackNoticeFactory = feedBackNoticeFactory;
            this.feedBackResultFactory = feedBackResultFactory;
            this.feedBackFactory = feedBackFactory;
            this.raygunFeedBack = raygunFeedBack;
            this.windowManager = windowManager;
        }

        protected override void OnAttach()
        {
            viewModel.OpenFeedBack = Command.Create(() => FeedBack());
        }

        void FeedBack()
        {
            if (raygunFeedBack.Enabled)
            {
                var feedBackViewModel = feedBackFactory();
                if (windowManager.ShowDialog(feedBackViewModel) == true)
                {
                    var result = feedBackResultFactory();
                    result.SetResult(feedBackViewModel.Success);
                    windowManager.ShowDialog(result);
                }
            }
            else
            {
                windowManager.ShowDialog(feedBackNoticeFactory());
            }
        }

        readonly Func<FeedBackViewModel> feedBackFactory;
        readonly Func<FeedBackNoticeViewModel> feedBackNoticeFactory;
        readonly Func<FeedBackResultViewModel> feedBackResultFactory;
        readonly RaygunFeedback raygunFeedBack;
        readonly IWindowManagerEx windowManager;
    }
}