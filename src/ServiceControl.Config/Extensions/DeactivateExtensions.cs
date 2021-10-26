namespace ServiceControl.Config
{
    using System;
    using System.Threading.Tasks;
    using System.Windows.Threading;
    using Caliburn.Micro;

    static class DeactivateExtensions
    {
        public static void RunModal(this IDeactivate item)
        {
            try
            {
                var frame = new DispatcherFrame();
                item.Deactivated += (sender, e) =>
                {
                    frame.Continue = false;
                    return Task.CompletedTask;
                };
                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                throw new Exception("Modal frame exception. Check inner exception for details.", ex.GetBaseException());
            }
        }
    }
}