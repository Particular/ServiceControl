namespace ServiceControl.Config.Framework.Rx
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;

    public partial class RxConductor<T> : RxConductorBaseWithActiveItem<T> where T : class
    {
        public override async Task ActivateItem(T item)
        {
            if (item != null && item.Equals(ActiveItem))
            {
                if (IsActive)
                {
                    await ScreenExtensions.TryActivateAsync(item);
                    OnActivationProcessed(item, true);
                }

                return;
            }

            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem });
            if (result.CloseCanOccur)
            {
                await ChangeActiveItem(item, true);
            }
            else
            {
                OnActivationProcessed(item, false);
            }
        }

        public override async Task DeactivateItem(T item, bool close)
        {
            if (item == null || !item.Equals(ActiveItem))
            {
                return;
            }

            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem });
            if (result.CloseCanOccur)
            {
                await ChangeActiveItem(default, close);
            }
        }

        public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem }, cancellationToken);
            return result.CloseCanOccur;
        }

        protected override Task OnActivate() => ScreenExtensions.TryActivateAsync(ActiveItem);

        protected override Task OnDeactivate(bool close) => ScreenExtensions.TryDeactivateAsync(ActiveItem, close);

        public override IEnumerable<T> GetChildren()
        {
            return new[] { ActiveItem };
        }
    }
}