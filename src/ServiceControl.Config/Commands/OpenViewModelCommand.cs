using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Commands;

namespace ServiceControl.Config.Commands
{
    class OpenViewModelCommand<T> : AbstractCommand<object>
    {
        private readonly T screen;
        private readonly IWindowManagerEx windowManager;

        public OpenViewModelCommand(IWindowManagerEx windowManager, T screen)
        {
            this.screen = screen;
            this.windowManager = windowManager;
        }

        public override void Execute(object obj)
        {
            windowManager.ShowDialog(screen);
        }
    }
}