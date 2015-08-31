using System;
using System.Windows.Threading;
using Caliburn.Micro;

namespace ServiceControl.Config
{
    static class DeactivateExtensions
    {
        public static void RunModal(this IDeactivate item)
        {
            try
            {
                var frame = new DispatcherFrame();
                item.Deactivated += (sender, e) => { frame.Continue = false; };
                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                throw new ModalException("Modal frame exception. Check inner exception for details.", ex.GetBaseException());
            }
        }
    }

    [Serializable]
    public class ModalException : Exception
    {
        public ModalException()
        {
        }

        public ModalException(string message) : base(message)
        {
        }

        public ModalException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ModalException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}