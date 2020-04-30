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
                    lookup = breakdowns.Values.ToArray()
                        .GroupBy(b => endpointNameExtractor(b))
                        .ToDictionary(g => g.Key, g => (IEnumerable<BreakdownT>)g.Select(i => i).ToArray());
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

        public IReadOnlyDictionary<string, IEnumerable<BreakdownT>> GetGroupedByEndpointName()
        {
            return lookup;
        }

        public IEnumerable<BreakdownT> GetForEndpointName(string endpointName)
        {
            if (lookup.TryGetValue(endpointName, out var endpointBreakdowns))
            {
                return endpointBreakdowns;
            }

            return emptyResult;
        }

        public void RemoveBreakdown(BreakdownT breakdown)
        {
            lock (@lock)
            {
                if (breakdowns.Remove(breakdown))
                {
                    lookup = breakdowns.Values.ToArray()
                            .GroupBy(b => endpointNameExtractor(b))
                            .ToDictionary(g => g.Key, g => (IEnumerable<BreakdownT>)g.Select(i => i).ToArray());
                }
            }
        }

        Dictionary<BreakdownT, BreakdownT> breakdowns = new Dictionary<BreakdownT, BreakdownT>();
        volatile Dictionary<string, IEnumerable<BreakdownT>> lookup = new Dictionary<string, IEnumerable<BreakdownT>>();
        object @lock = new object();

        Func<BreakdownT, string> endpointNameExtractor;

        static IEnumerable<BreakdownT> emptyResult = new BreakdownT[0];
    }
}