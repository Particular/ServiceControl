namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
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
            lock (@lock)
            {
                if (AddBreakdown(breakdown, breakdowns))
                {
                    UpdateLookups();
                }
            }
        }

        protected virtual bool AddBreakdown(BreakdownT breakdown, Dictionary<BreakdownT, BreakdownT> existingBreakdowns)
        {
            if (existingBreakdowns.ContainsKey(breakdown))
            {
                return false;
            }

            existingBreakdowns.Add(breakdown, breakdown);

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

            return Array.Empty<BreakdownT>();
        }

        public void RemoveBreakdowns(IEnumerable<BreakdownT> breakdownsToRemove)
        {
            lock (@lock)
            {
                foreach (var breakdown in breakdownsToRemove)
                {
                    breakdowns.Remove(breakdown);
                }
                UpdateLookups();
            }
        }

        void UpdateLookups()
        {
            lookup = breakdowns.Values
                        .GroupBy(b => endpointNameExtractor(b))
                        .ToDictionary(g => g.Key, g => g.Select(i => i).ToArray());
        }

        Dictionary<BreakdownT, BreakdownT> breakdowns = new Dictionary<BreakdownT, BreakdownT>();
        volatile Dictionary<string, BreakdownT[]> lookup = new Dictionary<string, BreakdownT[]>();
        object @lock = new object();

        Func<BreakdownT, string> endpointNameExtractor;
    }
}