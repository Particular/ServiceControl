namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class BreakdownRegistry<BreakdownT>
    {
        protected BreakdownRegistry(Func<BreakdownT, string> endpointNameExtractor)
        {
            this.endpointNameExtractor = endpointNameExtractor;
        }

        public void Record(BreakdownT breakdown)
        {
            if (AddBreakdown(breakdown, breakdowns))
            {
                UpdateLookups();
            }
        }

        protected virtual bool AddBreakdown(BreakdownT breakdown, ConcurrentDictionary<BreakdownT, BreakdownT> existingBreakdowns)
        {
            if (existingBreakdowns.ContainsKey(breakdown))
            {
                return false;
            }

            existingBreakdowns.TryAdd(breakdown, breakdown);

            return true;
        }

        public IReadOnlyDictionary<string, BreakdownT[]> GetGroupedByEndpointName()
        {
            return lookup;
        }

        public BreakdownT[] GetForEndpointName(string endpointName)
        {
            if (lookup.TryGetValue(endpointName, out var endpointBreakdowns))
            {
                return endpointBreakdowns;
            }

            return [];
        }

        public void RemoveBreakdowns(IEnumerable<BreakdownT> breakdownsToRemove)
        {
            foreach (var breakdown in breakdownsToRemove)
            {
                breakdowns.Remove(breakdown, out _);
            }
            UpdateLookups();
        }

        void UpdateLookups()
        {
            lookup = new ConcurrentDictionary<string, BreakdownT[]>(breakdowns.Values
                .GroupBy(b => endpointNameExtractor(b))
                .ToDictionary(g => g.Key, g => g.Select(i => i).ToArray()));
        }

        ConcurrentDictionary<BreakdownT, BreakdownT> breakdowns = [];
        ConcurrentDictionary<string, BreakdownT[]> lookup = [];

        Func<BreakdownT, string> endpointNameExtractor;
    }
}