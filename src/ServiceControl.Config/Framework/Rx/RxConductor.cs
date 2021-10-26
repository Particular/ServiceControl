namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

    public partial class RxConductor<T> : RxConductorBaseWithActiveItem<T> where T : class
    {
        public override void ActivateItem(T item)
        {
            if (item != null && item.Equals(ActiveItem))
            {
                if (IsActive)
                {
                    ScreenExtensions.TryActivate(item);
                    OnActivationProcessed(item, true);
                }

                return;
            }

            CloseStrategy.Execute(new[] { ActiveItem }, (canClose, items) =>
              {
                  if (canClose)
                  {
                      ChangeActiveItem(item, true);
                  }
                  else
                  {
                      OnActivationProcessed(item, false);
                  }
              });
        }

        public override void DeactivateItem(T item, bool close)
        {
            if (item == null || !item.Equals(ActiveItem))
            {
                return;
            }

            CloseStrategy.Execute(new[] { ActiveItem }, (canClose, items) =>
              {
                  if (canClose)
                  {
                      ChangeActiveItem(default, close);
                  }
              });
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