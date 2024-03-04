namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System.Collections.Generic;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;

    static class ThroughputCollectorTestData
    {
        public static List<Endpoint> GetEndpointsThroughput(Broker broker)
        {
            var endpoints = new List<Endpoint>();

            //audit
            for (var e = 0; e < 10; e++)
            {
                var endpointThroughput = new List<EndpointThroughput>();

                for (var t = 0; t < 50; t++)
                {
                    if (t % 5 != 0)
                    {
                        endpointThroughput.Add(new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-t), TotalThroughput = t * 2 });
                    }
                }

                endpoints.Add(new Endpoint
                {
                    Name = $"Endpoint{e}",
                    Queue = $"Queue{e}",
                    ThroughputSource = ThroughputSource.Audit,
                    DailyThroughput = endpointThroughput,
                });
            }

            //monitoring
            for (var e = 10; e < 20; e++)
            {
                var endpointThroughput = new List<EndpointThroughput>();

                for (var t = 0; t < 50; t++)
                {
                    if (t % 2 != 0)
                    {
                        endpointThroughput.Add(new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-t), TotalThroughput = t * 2 });
                    }
                }

                endpoints.Add(new Endpoint
                {
                    Name = $"Endpoint{e}",
                    Queue = $"Queue{e}",
                    ThroughputSource = ThroughputSource.Monitoring,
                    DailyThroughput = endpointThroughput,
                });
            }

            //broker
            if (broker != Broker.None)
            {
                for (var e = 20; e < 40; e++)
                {
                    var endpointThroughput = new List<EndpointThroughput>();

                    for (var t = 0; t < 50; t++)
                    {
                        if (t % 10 != 0)
                        {
                            endpointThroughput.Add(new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-t), TotalThroughput = t * 2 });
                        }
                    }

                    endpoints.Add(new Endpoint
                    {
                        Name = $"Endpoint{e}",
                        Queue = $"Queue{e}",
                        ThroughputSource = ThroughputSource.Broker,
                        DailyThroughput = endpointThroughput,
                    });
                }
            }

            endpoints.Add(new Endpoint
            {
                Name = $"error",
                Queue = $"error",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 10 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 10 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = $"audit",
                Queue = $"audit",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 1000 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 1000 }],
            });

            return endpoints;
        }
    }
}