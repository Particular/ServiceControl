namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Features;
    using Logging;
    using Routing;

    class InstanceMappingFileMonitor : FeatureStartupTask
    {
        public InstanceMappingFileMonitor(TimeSpan checkInterval, IAsyncTimer timer, IInstanceMappingLoader loader, EndpointInstances endpointInstances)
        {
            this.checkInterval = checkInterval;
            this.timer = timer;
            this.loader = loader;
            this.endpointInstances = endpointInstances;
        }

        internal Task Start(IMessageSession session)
        {
            return OnStart(session);
        }

        protected override Task OnStart(IMessageSession session)
        {
            timer.Start(() =>
            {
                ReloadData();
                return TaskEx.CompletedTask;
            }, checkInterval, ex => log.Error("Unable to update instance mapping information because the instance mapping file couldn't be read.", ex));
            return TaskEx.CompletedTask;
        }

        public void ReloadData()
        {
            try
            {
                var doc = loader.Load();
                var instances = parser.Parse(doc);
                LogChanges(instances);
                endpointInstances.AddOrReplaceInstances("InstanceMappingFile", instances);
            }
            catch (Exception exception)
            {
                throw new Exception($"An error occurred while reading the endpoint instance mapping ({loader}). See the inner exception for more details.", exception);
            }
        }

        void LogChanges(List<EndpointInstance> instances)
        {
            var output = new StringBuilder();
            var hasChanges = false;

            var instancesPerEndpoint = instances.GroupBy(i => i.Endpoint).ToDictionary(g => g.Key, g => g.ToArray());

            output.AppendLine($"Updating instance mapping table from '{loader}':");

            foreach (var endpoint in instancesPerEndpoint)
            {
                if (previousInstances.TryGetValue(endpoint.Key, out var existingInstances))
                {
                    var newInstances = endpoint.Value.Except(existingInstances).Count();
                    var removedInstances = existingInstances.Except(endpoint.Value).Count();

                    if (newInstances > 0 || removedInstances > 0)
                    {
                        output.AppendLine($"Updated endpoint '{endpoint.Key}': +{Instances(newInstances)}, -{Instances(removedInstances)}");
                        hasChanges = true;
                    }
                }
                else
                {
                    output.AppendLine($"Added endpoint '{endpoint.Key}' with {Instances(endpoint.Value.Length)}");
                    hasChanges = true;
                }
            }

            foreach (var removedEndpoint in previousInstances.Keys.Except(instancesPerEndpoint.Keys))
            {
                output.AppendLine($"Removed all instances of endpoint '{removedEndpoint}'");
                hasChanges = true;
            }

            if (hasChanges)
            {
                log.Info(output.ToString());
            }

            previousInstances = instancesPerEndpoint;
        }

        static string Instances(int count)
        {
            return count > 1 ? $"{count} instances" : $"{count} instance";
        }

        protected override Task OnStop(IMessageSession session) => timer.Stop();

        TimeSpan checkInterval;
        IInstanceMappingLoader loader;
        EndpointInstances endpointInstances;
        InstanceMappingFileParser parser = new InstanceMappingFileParser();
        IAsyncTimer timer;
        IDictionary<string, EndpointInstance[]> previousInstances = new Dictionary<string, EndpointInstance[]>(0);

        static ILog log = LogManager.GetLogger(typeof(InstanceMappingFileMonitor));
    }
}