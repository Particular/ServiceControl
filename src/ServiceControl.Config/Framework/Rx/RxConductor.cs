using System;
using System.Collections.Generic;
using Caliburn.Micro;

namespace ServiceControl.Config.Framework.Rx
{
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
                    ChangeActiveItem(item, true);
                else OnActivationProcessed(item, false);
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
                    ChangeActiveItem(default(T), close);
            });
        }

        public override void CanClose(Action<bool> callback)
        {
            CloseStrategy.Execute(new[] { ActiveItem }, (canClose, items) => callback(canClose));
        }

        protected override void OnActivate()
        {
            ScreenExtensions.TryActivate(ActiveItem);
        }

        protected override void OnDeactivate(bool close)
        {
            ScreenExtensions.TryDeactivate(ActiveItem, close);
        }

        public override IEnumerable<T> GetChildren()
        {
            return new[] { ActiveItem };
        }
    }
}