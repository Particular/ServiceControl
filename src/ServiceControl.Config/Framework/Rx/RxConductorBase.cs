namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;

    public abstract class RxConductorBase<T> : RxScreen, IConductor, IParent<T> where T : class
    {
        public ICloseStrategy<T> CloseStrategy
        {
            get { return closeStrategy ?? (closeStrategy = new DefaultCloseStrategy<T>()); }
            set { closeStrategy = value; }
        }

        Task IConductor.ActivateItemAsync(object item, CancellationToken cancellationToken)
        {
            return ActivateItem((T)item);
        }

        Task IConductor.DeactivateItemAsync(object item, bool close, CancellationToken cancellationToken)
        {
            return DeactivateItem((T)item, close);
        }

        IEnumerable IParent.GetChildren()
        {
            return GetChildren();
        }

        public event EventHandler<ActivationProcessedEventArgs> ActivationProcessed = (sender, e) => { };

        public abstract IEnumerable<T> GetChildren();

        public abstract Task ActivateItem(T item);

        public abstract Task DeactivateItem(T item, bool close);

        protected virtual void OnActivationProcessed(T item, bool success)
        {
            if (item == null)
            {
                return;
            }

            ActivationProcessed(this, new ActivationProcessedEventArgs
            {
                Item = item,
                Success = success
            });
        }

        protected virtual T EnsureItem(T newItem)
        {
            if (newItem is IChild node && node.Parent != this)
            {
                node.Parent = this;
            }

            return newItem;
        }

        ICloseStrategy<T> closeStrategy;
    }
}