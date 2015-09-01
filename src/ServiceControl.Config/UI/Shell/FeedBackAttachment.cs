namespace ServiceControl.Config.UI.Shell
{
    using System;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.UI.FeedBack;
    using ServiceControl.Config.Validation;

    class FeedBackAttachment : Attachment<ShellViewModel>
    {
        private readonly Func<FeedBackViewModel> feedBackFactory;
        private readonly Func<FeedBackNoticeViewModel> feedBackNoticeFactory;
        private readonly Func<FeedBackResultViewModel> feedBackResultFactory;
        private readonly RaygunFeedback raygunFeedBack;
        private readonly IWindowManagerEx windowManager;

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
                feedBackViewModel.ValidationTemplate = new ValidationTemplate(feedBackViewModel);
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
    }
}