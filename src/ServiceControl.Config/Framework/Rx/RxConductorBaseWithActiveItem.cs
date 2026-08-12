namespace ServiceControl.Config.Framework.Rx
{
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;

    public abstract class RxConductorBaseWithActiveItem<T> : RxConductorBase<T>, IConductActiveItem where T : class
    {
        public T ActiveItem
        {
            get { return activeItem; }
            set { ActivateItem(value).Wait(); }
        }

        object IHaveActiveItem.ActiveItem
        {
            get { return ActiveItem; }
            set { ActiveItem = (T)value; }
        }

        protected virtual async Task ChangeActiveItem(T newItem, bool closePrevious, CancellationToken cancellationToken = default)
        {
            await ScreenExtensions.TryDeactivateAsync(activeItem, closePrevious, cancellationToken);

            newItem = EnsureItem(newItem);

            if (IsActive)
            {
                await ScreenExtensions.TryActivateAsync(newItem, cancellationToken);
            }

            activeItem = newItem;
            NotifyOfPropertyChange("ActiveItem");
            OnActivationProcessed(activeItem, true);
        }

        T activeItem;
    }
}