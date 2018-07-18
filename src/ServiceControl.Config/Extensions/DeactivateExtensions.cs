using System;
using System.Windows.Threading;
using Caliburn.Micro;

namespace ServiceControl.Config
{
    static class DeactivateExtensions
    {
        public static void RunModal(this IDeactivate item)
        {
            //try
            {
                var frame = new DispatcherFrame();
                item.Deactivated += (sender, e) => { frame.Continue = false; };
                Dispatcher.PushFrame(frame);
            }
            //catch (Exception ex)
            //{
            //    throw new Exception("Modal frame exception. Check inner exception for details.", ex.GetBaseException());
            //}
        }
    }

}