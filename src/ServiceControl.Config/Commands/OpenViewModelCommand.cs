namespace ServiceControl.Config.Commands
{
    using System.Threading.Tasks;
    using Framework;
    using Framework.Commands;

    class OpenViewModelCommand<T> : AwaitableAbstractCommand<object>
    {
        public OpenViewModelCommand(IServiceControlWindowManager windowManager, T screen)
        {
            this.screen = screen;
            this.windowManager = windowManager;
        }

        public override Task ExecuteAsync(object obj) => windowManager.ShowDialogAsync(screen);

        readonly T screen;
        readonly IServiceControlWindowManager windowManager;
    }
}