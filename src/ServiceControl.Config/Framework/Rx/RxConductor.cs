namespace ServiceControl.Config.Framework.Rx
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;

    public partial class RxConductor<T> : RxConductorBaseWithActiveItem<T> where T : class
    {
        public override async Task ActivateItem(T item, CancellationToken cancellationToken = default)
        {
            if (item != null && item.Equals(ActiveItem))
            {
                if (IsActive)
                {
                    await ScreenExtensions.TryActivateAsync(item, cancellationToken);
                    OnActivationProcessed(item, true);
                }

                return;
            }

            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem }, cancellationToken);
            if (result.CloseCanOccur)
            {
                await ChangeActiveItem(item, true, cancellationToken);
            }
            else
            {
                OnActivationProcessed(item, false);
            }
        }

        public override async Task DeactivateItem(T item, bool close, CancellationToken cancellationToken = default)
        {
            if (item == null || !item.Equals(ActiveItem))
            {
                return;
            }

            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem }, cancellationToken);
            if (result.CloseCanOccur)
            {
                await ChangeActiveItem(default, close, cancellationToken);
            }
        }

        public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        {
            var result = await CloseStrategy.ExecuteAsync(new[] { ActiveItem }, cancellationToken);
            return result.CloseCanOccur;
        }

        protected override Task OnActivate(CancellationToken cancellationToken = default) => ScreenExtensions.TryActivateAsync(ActiveItem, cancellationToken);

        protected override Task OnDeactivate(bool close, CancellationToken cancellationToken = default) => ScreenExtensions.TryDeactivateAsync(ActiveItem, close, cancellationToken);

        public override IEnumerable<T> GetChildren()
        {
            return new[] { ActiveItem };
        }
    }
}