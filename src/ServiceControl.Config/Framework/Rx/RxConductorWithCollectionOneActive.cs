namespace ServiceControl.Config.Framework.Rx
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using EnumerableExtensions = EnumerableExtensions;

    public partial class RxConductor<T>
    {
        public class OneActive : RxConductorBaseWithActiveItem<T>
        {
            public OneActive()
            {
                items.CollectionChanged += (s, e) =>
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            EnumerableExtensions.Apply(e.NewItems.OfType<IChild>(), x => x.Parent = this);
                            break;

                        case NotifyCollectionChangedAction.Remove:
                            EnumerableExtensions.Apply(e.OldItems.OfType<IChild>(), x => x.Parent = null);
                            break;

                        case NotifyCollectionChangedAction.Replace:
                            EnumerableExtensions.Apply(e.NewItems.OfType<IChild>(), x => x.Parent = this);
                            EnumerableExtensions.Apply(e.OldItems.OfType<IChild>(), x => x.Parent = null);
                            break;

                        case NotifyCollectionChangedAction.Reset:
                            EnumerableExtensions.Apply(items.OfType<IChild>(), x => x.Parent = this);
                            break;
                        case NotifyCollectionChangedAction.Move:
                        default:
                            break;
                    }
                };
            }

            public IObservableCollection<T> Items => items;

            public override IEnumerable<T> GetChildren()
            {
                return items;
            }

            public override async Task ActivateItem(T item)
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

                await ChangeActiveItem(item, false);
            }

            public override async Task DeactivateItem(T item, bool close)
            {
                if (item == null)
                {
                    return;
                }

                if (!close)
                {
                    ScreenExtensions.TryDeactivate(item, false);
                }
                else
                {
                    var result = await CloseStrategy.ExecuteAsync(new[] { item });
                    if (result.CloseCanOccur)
                    {
                        await CloseItemCore(item);
                    }
                }
            }

            async Task CloseItemCore(T item)
            {
                if (item.Equals(ActiveItem))
                {
                    var index = items.IndexOf(item);
                    var next = DetermineNextItemToActivate(items, index);

                   await ChangeActiveItem(next, true);
                }
                else
                {
                    await ScreenExtensions.TryDeactivateAsync(item, true);
                }

                items.Remove(item);
            }

            protected virtual T DetermineNextItemToActivate(IList<T> list, int lastIndex)
            {
                var toRemoveAt = lastIndex - 1;

                if (toRemoveAt == -1 && list.Count > 1)
                {
                    return list[1];
                }

                if (toRemoveAt > -1 && toRemoveAt < list.Count - 1)
                {
                    return list[toRemoveAt];
                }

                return default;
            }

            public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken)
            {
                var result = await CloseStrategy.ExecuteAsync(items.ToList(), cancellationToken);
                var canClose = result.CloseCanOccur;
                var closable = result.Children.ToList();

                if (!canClose && closable.Any())
                {
                    if (closable.Contains(ActiveItem))
                    {
                        var list = items.ToList();
                        var next = ActiveItem;
                        do
                        {
                            var previous = next;
                            next = DetermineNextItemToActivate(list, list.IndexOf(previous));
                            list.Remove(previous);
                        }
                        while (closable.Contains(next));

                        var previousActive = ActiveItem;
                        await ChangeActiveItem(next, true);
                        items.Remove(previousActive);

                        var stillToClose = closable.ToList();
                        stillToClose.Remove(previousActive);
                        closable = stillToClose;
                    }

                    await Task.WhenAll(
                        from deactivatable in closable.OfType<IDeactivate>()
                        select deactivatable.DeactivateAsync(true)
                    );

                    items.RemoveRange(closable);
                }

                return canClose;
            }

            protected override Task OnActivate() => ScreenExtensions.TryActivateAsync(ActiveItem);

            protected override async Task OnDeactivate(bool close)
            {
                if (close)
                {
                    foreach (var item in items)
                    {
                        if (item is IDeactivate deactivatable)
                        {
                            await deactivatable.DeactivateAsync(true);
                        }
                    }
                    items.Clear();
                }
                else
                {
                    await ScreenExtensions.TryDeactivateAsync(ActiveItem, false);
                }
            }

            protected override T EnsureItem(T newItem)
            {
                if (newItem == null)
                {
                    newItem = DetermineNextItemToActivate(items, ActiveItem != null ? items.IndexOf(ActiveItem) : 0);
                }
                else
                {
                    var index = items.IndexOf(newItem);

                    if (index == -1)
                    {
                        items.Add(newItem);
                    }
                    else
                    {
                        newItem = items[index];
                    }
                }

                return base.EnsureItem(newItem);
            }

            readonly BindableCollection<T> items = new BindableCollection<T>();
        }
    }
}