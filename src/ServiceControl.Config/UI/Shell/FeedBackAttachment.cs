namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Threading.Tasks;
    using FeedBack;
    using Framework;

    class FeedBackAttachment : Attachment<ShellViewModel>
    {
        public FeedBackAttachment(
            RaygunFeedback raygunFeedBack,
            IServiceControlWindowManager windowManager,
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
            viewModel.OpenFeedBack = Command.Create(async () => await FeedBack());
        }

        async Task FeedBack()
        {
            if (raygunFeedBack.Enabled)
            {
                var feedBackViewModel = feedBackFactory();

                if (await windowManager.ShowDialogAsync(feedBackViewModel) == true)
                {
                    var result = feedBackResultFactory();
                    result.SetResult(feedBackViewModel.Success);
                    await windowManager.ShowDialogAsync(result);
                }
            }
            else
            {
                await windowManager.ShowDialogAsync(feedBackNoticeFactory());
            }
        }

        readonly Func<FeedBackViewModel> feedBackFactory;
        readonly Func<FeedBackNoticeViewModel> feedBackNoticeFactory;
        readonly Func<FeedBackResultViewModel> feedBackResultFactory;
        readonly RaygunFeedback raygunFeedBack;
        readonly IServiceControlWindowManager windowManager;
    }
}