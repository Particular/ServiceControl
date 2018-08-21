namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Caliburn.Micro;

    public abstract class RxConductorBase<T> : RxScreen, IConductor, IParent<T> where T : class
    {
        public ICloseStrategy<T> CloseStrategy
        {
            get { return closeStrategy ?? (closeStrategy = new DefaultCloseStrategy<T>()); }
            set { closeStrategy = value; }
        }

        void IConductor.ActivateItem(object item)
        {
            ActivateItem((T)item);
        }

        void IConductor.DeactivateItem(object item, bool close)
        {
            DeactivateItem((T)item, close);
        }

        IEnumerable IParent.GetChildren()
        {
            return GetChildren();
        }

        public event EventHandler<ActivationProcessedEventArgs> ActivationProcessed = delegate { };

        public abstract IEnumerable<T> GetChildren();

        public abstract void ActivateItem(T item);

        public abstract void DeactivateItem(T item, bool close);

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