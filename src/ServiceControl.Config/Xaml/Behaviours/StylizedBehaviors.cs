﻿namespace ServiceControl.Config.Xaml.Behaviours
{
    using System.Windows;
    using Microsoft.Xaml.Behaviors;
    using PropertyChanged;

    public class StylizedBehaviors
    {
        public static StylizedBehaviorCollection GetBehaviors(DependencyObject uie)
        {
            return (StylizedBehaviorCollection)uie.GetValue(BehaviorsProperty);
        }

        public static void SetBehaviors(DependencyObject uie, StylizedBehaviorCollection value)
        {
            uie.SetValue(BehaviorsProperty, value);
        }

        static Behavior GetOriginalBehavior(DependencyObject obj)
        {
            return obj.GetValue(OriginalBehaviorProperty) as Behavior;
        }

        static int GetIndexOf(BehaviorCollection itemBehaviors, Behavior behavior)
        {
            var index = -1;

            var orignalBehavior = GetOriginalBehavior(behavior);

            for (var i = 0; i < itemBehaviors.Count; i++)
            {
                var currentBehavior = itemBehaviors[i];

                if (currentBehavior == behavior
                    || currentBehavior == orignalBehavior)
                {
                    index = i;
                    break;
                }

                var currentOrignalBehavior = GetOriginalBehavior(currentBehavior);

                if (currentOrignalBehavior == behavior
                    || currentOrignalBehavior == orignalBehavior)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        static void OnPropertyChanged(DependencyObject dpo, DependencyPropertyChangedEventArgs e)
        {
            if (dpo is not UIElement uie)
            {
                return;
            }

            var itemBehaviors = Interaction.GetBehaviors(uie);

            var newBehaviors = e.NewValue as StylizedBehaviorCollection;
            var oldBehaviors = e.OldValue as StylizedBehaviorCollection;

            if (newBehaviors == oldBehaviors)
            {
                return;
            }

            if (oldBehaviors != null)
            {
                foreach (var behavior in oldBehaviors)
                {
                    var index = GetIndexOf(itemBehaviors, behavior);

                    if (index >= 0)
                    {
                        itemBehaviors.RemoveAt(index);
                    }
                }
            }

            if (newBehaviors != null)
            {
                foreach (var behavior in newBehaviors)
                {
                    var index = GetIndexOf(itemBehaviors, behavior);

                    if (index < 0)
                    {
                        var clone = (Behavior)behavior.Clone();
                        SetOriginalBehavior(clone, behavior);
                        itemBehaviors.Add(clone);
                    }
                }
            }
        }

        static void SetOriginalBehavior(DependencyObject obj, Behavior value)
        {
            obj.SetValue(OriginalBehaviorProperty, value);
        }

        static readonly DependencyProperty OriginalBehaviorProperty = DependencyProperty.RegisterAttached(@"OriginalBehaviorInternal", typeof(Behavior), typeof(StylizedBehaviors), new UIPropertyMetadata(null));

        public static readonly DependencyProperty BehaviorsProperty = DependencyProperty.RegisterAttached(
            @"Behaviors",
            typeof(StylizedBehaviorCollection),
            typeof(StylizedBehaviors),
            new FrameworkPropertyMetadata(null, OnPropertyChanged));
    }

    [DoNotNotify]
    public class StylizedBehaviorCollection : FreezableCollection<Behavior>
    {
        protected override Freezable CreateInstanceCore()
        {
            return new StylizedBehaviorCollection();
        }
    }
}