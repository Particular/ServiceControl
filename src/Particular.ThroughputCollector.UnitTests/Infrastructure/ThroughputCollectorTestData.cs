namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System.Collections.Generic;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;

    static class ThroughputCollectorTestData
    {
        public const string EndpointNameWithNoThroughput = "EndpointNoThroughput";
        public const string EndpointNameWithMultiIndicators = "EndpointMultiIndicators";

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
                    ThroughputSource = ThroughputSource.Audit,
                    DailyThroughput = endpointThroughput,
                });
            }

            //monitoring
            for (var e = 5; e < 25; e++)
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
                    ThroughputSource = ThroughputSource.Monitoring,
                    DailyThroughput = endpointThroughput,
                });
            }

            //broker
            if (broker != Broker.ServiceControl)
            {
                for (var e = 10; e < 50; e++)
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
                        ThroughputSource = ThroughputSource.Broker,
                        DailyThroughput = endpointThroughput,
                    });
                }
            }

            endpoints.Add(new Endpoint
            {
                Name = "error",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 10 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 10 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = "audit",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 1000 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 1000 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = "audit",
                ThroughputSource = ThroughputSource.Broker,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 1000 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 1000 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = "Particular.ServiceControl",
                ThroughputSource = ThroughputSource.Audit,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 5000 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 1000 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = "Particular.ServiceControl",
                ThroughputSource = ThroughputSource.Broker,
                DailyThroughput = [new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddDays(-1), TotalThroughput = 5000 }, new EndpointThroughput { DateUTC = System.DateTime.UtcNow.Date.AddMonths(-1), TotalThroughput = 1000 }],
            });

            endpoints.Add(new Endpoint
            {
                Name = EndpointNameWithNoThroughput,
                ThroughputSource = ThroughputSource.Audit
            });

            endpoints.Add(new Endpoint
            {
                Name = EndpointNameWithNoThroughput,
                ThroughputSource = ThroughputSource.Broker
            });

            endpoints.Add(new Endpoint
            {
                Name = EndpointNameWithMultiIndicators,
                ThroughputSource = ThroughputSource.Broker,
                UserIndicatedSendOnly = true,
                UserIndicatedToIgnore = true,
            });

            endpoints.Add(new Endpoint
            {
                Name = EndpointNameWithMultiIndicators,
                ThroughputSource = ThroughputSource.Audit,
                UserIndicatedSendOnly = false,
                UserIndicatedToIgnore = false,
                EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()]
            });

            return endpoints;
        }
    }
}