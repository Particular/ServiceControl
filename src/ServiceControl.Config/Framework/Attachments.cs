namespace ServiceControl.Config.Framework
{
    interface IAttachment
    {
        void AttachTo(object obj);
    }

    public abstract class Attachment<T> : IAttachment
    {
        void IAttachment.AttachTo(object obj)
        {
            viewModel = (T)obj;
            OnAttach();
        }

        protected abstract void OnAttach();
        protected T viewModel;
    }
}