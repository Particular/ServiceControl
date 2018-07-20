namespace ServiceControl.Config.Commands
{
    using Framework;
    using Framework.Commands;

    class OpenViewModelCommand<T> : AbstractCommand<object>
    {
        public OpenViewModelCommand(IWindowManagerEx windowManager, T screen)
        {
            this.screen = screen;
            this.windowManager = windowManager;
        }

        public override void Execute(object obj)
        {
            windowManager.ShowDialog(screen);
        }

        readonly T screen;
        readonly IWindowManagerEx windowManager;
    }
}