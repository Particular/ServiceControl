namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public abstract class BreakdownRegistry<TBreakdown>(Func<TBreakdown, string> endpointNameExtractor)
    {
        public void Record(TBreakdown breakdown) => AddBreakdown(breakdown, breakdowns);

        protected virtual bool AddBreakdown(TBreakdown breakdown, ConcurrentDictionary<TBreakdown, TBreakdown> existingBreakdowns) => existingBreakdowns.TryAdd(breakdown, breakdown);

        public IReadOnlyDictionary<string, TBreakdown[]> GetGroupedByEndpointName() =>
            breakdowns.Values
                .GroupBy(endpointNameExtractor)
                .ToDictionary(g => g.Key, g => g.Select(i => i).ToArray());

        public TBreakdown[] GetForEndpointName(string endpointName) => GetGroupedByEndpointName().TryGetValue(endpointName, out var endpointBreakdowns) ? endpointBreakdowns : [];

        protected void RemoveBreakdowns(IEnumerable<TBreakdown> breakdownsToRemove)
        {
            foreach (var breakdown in breakdownsToRemove)
            {
                _ = breakdowns.Remove(breakdown, out _);
            }
        }

        readonly ConcurrentDictionary<TBreakdown, TBreakdown> breakdowns = [];
    }
}