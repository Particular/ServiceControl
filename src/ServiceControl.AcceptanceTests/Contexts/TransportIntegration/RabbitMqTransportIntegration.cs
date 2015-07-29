namespace ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus;

    public class RabbitMqTransportIntegration : ITransportIntegration
    {
        public RabbitMqTransportIntegration()
        {
            ConnectionString = "host=localhost"; // Default connstr
        }

        public string Name { get { return "RabbitMq"; } }
        public Type Type { get { return typeof(RabbitMQ); } }
        public string TypeName { get { return "NServiceBus.RabbitMQ, NServiceBus.Transports.RabbitMQ"; } }
        public string ConnectionString { get; set; }

        public void OnEndpointShutdown()
        {
            // It is not possible to delete all queues and exchanges over the C# client
            // we need the management plugin and call the proper HTTP apis to get all queues
            // and exchanges for the given vhost
        }

        public void TearDown()
        {
            // let's cheat for now because we cannot access ConnectionStringInformation
            var match = Regex.Match(ConnectionString, string.Format("[^\\w]*{0}=(?<{0}>[^;]+)", "host"), RegexOptions.IgnoreCase);

            var username = match.Groups["UserName"].Success ? match.Groups["UserName"].Value : "guest";
            var password = match.Groups["Password"].Success ? match.Groups["Password"].Value : "guest";
            var virtualHost = match.Groups["VirtualHost"].Success ? match.Groups["VirtualHost"].Value : "%2f"; // %2f is the name of the default virtual host

            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(username, password),
            };
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(string.Format("http://{0}:15672/", match.Groups["host"].Value))
            };
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var exchangeDeletionTasks = DeleteExchanges(httpClient, virtualHost);
            var queueDeletionTasks = DeleteQueues(httpClient, virtualHost);
            var cleanupTasks = exchangeDeletionTasks.Union(queueDeletionTasks).ToArray();
            Task.WhenAll(cleanupTasks).Wait();

            foreach (var cleanup in cleanupTasks)
            {
                var responseMessage = cleanup.Result;
                try
                {
                    responseMessage.EnsureSuccessStatusCode();
                }
                catch (WebException)
                {
                    // TC has some weird problems when this code is executed inside the NUnit runner. It works on the agents as a 
                    // seperate console executed with the same credentials as the agent, it also works when executed inside VS on the agents
                    var requerstMessage = responseMessage.RequestMessage;
                    Console.WriteLine("Cleanup task failed for {0} {1}", requerstMessage.Method, requerstMessage.RequestUri);
                }
            }
        }

        static IEnumerable<Task<HttpResponseMessage>> DeleteQueues(HttpClient httpClient, string virtualHost)
        {
            var queueResult = httpClient.GetAsync(string.Format("api/queues/{0}", virtualHost)).Result;
            queueResult.EnsureSuccessStatusCode();
            var queues = JsonConvert.DeserializeObject<List<Queue>>(queueResult.Content.ReadAsStringAsync().Result);

            var queueDeletionTasks = new List<Task<HttpResponseMessage>>(queues.Count);
            queueDeletionTasks.AddRange(queues.Select(queue =>
            {
                var deletionTask = httpClient.DeleteAsync(string.Format("/api/queues/{0}/{1}", virtualHost, queue.Name));
                deletionTask.ContinueWith((t, o) => { Console.WriteLine("Deleted queue {0}.", queue.Name); }, null, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion);
                deletionTask.ContinueWith((t, o) => { Console.WriteLine("Failed to delete queue {0}.", queue.Name); }, null, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
                return deletionTask;
            }));
            return queueDeletionTasks;
        }

        static IEnumerable<Task<HttpResponseMessage>> DeleteExchanges(HttpClient httpClient, string virtualHost)
        {
            // Delete exchanges
            var exchangeResult = httpClient.GetAsync(string.Format("api/exchanges/{0}", virtualHost)).Result;
            exchangeResult.EnsureSuccessStatusCode();
            var allExchanges = JsonConvert.DeserializeObject<List<Exchange>>(exchangeResult.Content.ReadAsStringAsync().Result);
            var exchanges = FilterAllExchangesByExcludingInternalTheDefaultAndAmq(allExchanges);

            var exchangeDeletionTasks = new List<Task<HttpResponseMessage>>(exchanges.Count);
            exchangeDeletionTasks.AddRange(exchanges.Select(exchange =>
            {
                var deletionTask = httpClient.DeleteAsync(string.Format("/api/exchanges/{0}/{1}", virtualHost, exchange.Name));
                deletionTask.ContinueWith((t, o) => { Console.WriteLine("Deleted exchange {0}.", exchange.Name); }, null, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion);
                deletionTask.ContinueWith((t, o) => { Console.WriteLine("Failed to delete exchange {0}.", exchange.Name); }, null, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
                return deletionTask;
            }));
            return exchangeDeletionTasks;
        }

        static List<Exchange> FilterAllExchangesByExcludingInternalTheDefaultAndAmq(IEnumerable<Exchange> allExchanges)
        {
            return (from exchange in allExchanges
                let isInternal = exchange.Internal
                let name = exchange.Name.ToLowerInvariant()
                where !isInternal  // we should never delete rabbits internal exchanges
                where !name.StartsWith("amq.") // amq.* we shouldn't remove
                where name.Length > 0 // the default exchange which can't be deleted has a Name=string.Empty
                select exchange)
                .ToList();
        }

        private class Queue
        {
            public string Name { get; set; }
        }

        private class Exchange
        {
            public string Name { get; set; }
            public bool Internal { get; set; }
        }
    }
}
