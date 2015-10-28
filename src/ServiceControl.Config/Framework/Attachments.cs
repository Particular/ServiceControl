namespace ServiceControl.Config.Framework
{
    internal interface IAttachment
    {
        void AttachTo(object obj);
    }

    public abstract class Attachment<T> : IAttachment
    {
        protected T viewModel;

        protected abstract void OnAttach();

        void IAttachment.AttachTo(object obj)
        {
            viewModel = (T)obj;
            OnAttach();
        }
    }
}