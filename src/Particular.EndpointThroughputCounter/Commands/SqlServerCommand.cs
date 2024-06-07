using System.Collections.Frozen;
using System.CommandLine;
using Microsoft.Extensions.Logging.Abstractions;
using Particular.LicensingComponent.Report;
using ServiceControl.Transports;
using ServiceControl.Transports.SqlServer;
using QueueThroughput = Particular.LicensingComponent.Report.QueueThroughput;

internal class SqlServerCommand : BaseCommand
{
    private readonly string additionalCatalogs;
    private readonly List<IBrokerQueue> queuesNames = [];

    private static readonly Option<string> ConnectionString = new("--connectionString",
        "A connection string for SQL Server that has access to all NServiceBus queue tables");

    private static readonly Option<string> ConnectionStringSource = new("--connectionStringSource",
        "A file that contains multiple SQL Server connection strings, one connection string per line, for each database catalog that contains NServiceBus queue tables");

    private static readonly Option<string[]> AddCatalogs = new("--addCatalogs")
    {
        Description =
            "A list of additional database catalogs on the same server containing NServiceBus queue tables",
        Arity = ArgumentArity.OneOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    public static Command CreateCommand()
    {
        var command = new Command("sqlserver",
            "Measure endpoints in SQL Server transport using the direct query method");

        command.AddOption(ConnectionString);
        command.AddOption(ConnectionStringSource);
        command.AddOption(AddCatalogs);

        command.SetHandler(async context =>
        {
            var shared = SharedOptions.Parse(context);
            var cancellationToken = context.GetCancellationToken();

            var connString = context.ParseResult.GetValueForOption(ConnectionString);
            var additionalCatalogs = context.ParseResult.GetValueForOption(AddCatalogs);

            var runner = new SqlServerCommand(shared, connString!, additionalCatalogs ?? []);
            await runner.Run(cancellationToken);
        });

        return command;
    }

    private readonly string connectionString;
    private readonly SqlServerQuery query;
    public SqlServerCommand(SharedOptions shared, string connectionString, string[] additionalCatalogs) : base(shared)
    {
        this.connectionString = connectionString;
        this.additionalCatalogs = string.Join(",", additionalCatalogs);
        var transportSettings = new TransportSettings
        {
            ConnectionString = connectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        query = new SqlServerQuery(NullLogger<SqlServerQuery>.Instance, TimeProvider.System, transportSettings);
    }

    protected override Task Initialize(CancellationToken cancellationToken = default)
    {
        var dictionary = new Dictionary<string, string>
        {
            { SqlServerQuery.SqlServerSettings.ConnectionString, connectionString },
            { SqlServerQuery.SqlServerSettings.AdditionalCatalogs, additionalCatalogs }
        };
        query.Initialise(dictionary.ToFrozenDictionary());

        return Task.CompletedTask;
    }

    protected override async Task<QueueDetails> GetData(CancellationToken cancellationToken = default)
    {
        var waitingTasks = new List<Task<QueueThroughput>>();

        foreach (var queueName in queuesNames)
        {
            waitingTasks.Add(Exec(queueName));
        }

        var startTime = DateTimeOffset.UtcNow;
        var results = await Task.WhenAll(waitingTasks);

        return new QueueDetails
        {
            Queues = results,
            ScopeType = query.ScopeType,
            StartTime = startTime,
            EndTime = startTime.AddDays(1)
        };

        async Task<QueueThroughput> Exec(IBrokerQueue queueName)
        {
            var startDate = DateTime.UtcNow;
            var defaultStartDate = DateOnly.FromDateTime(startDate).AddDays(-30);
            var throughput = new List<ServiceControl.Transports.QueueThroughput>();

            await foreach (var queue in query.GetThroughputPerDay(queueName, defaultStartDate, cancellationToken))
            {
                throughput.Add(queue);
            }

            var result = new QueueThroughput
            {
                QueueName = queueName.QueueName,
                EndpointIndicators = queueName.EndpointIndicators.ToArray(),
                Scope = queueName.Scope ?? "",
                DailyThroughputFromBroker = [
                    new DailyThroughput
                    {
                        DateUTC = DateOnly.FromDateTime(startDate),
                        MessageCount = throughput.GroupBy(queueThroughput => queueThroughput.DateUTC)
                            .Select(queueThroughput => new DailyThroughput { DateUTC = queueThroughput.Key, MessageCount = queueThroughput
                                .Sum(throughput1 => throughput1.TotalThroughput) })
                            .Max(dailyThroughput => dailyThroughput.MessageCount)
                    }
                ]
            };

            return result;
        }
    }

    protected override async Task<EnvironmentDetails> GetEnvironment(CancellationToken cancellationToken = default)
    {
        await foreach (var queueName in query.GetQueueNames(cancellationToken))
        {
            queuesNames.Add(queueName);
        }

        return new EnvironmentDetails
        {
            MessageTransport = query.MessageTransport,
            ReportMethod = "SqlServerQuery",
            QueueNames = queuesNames.Select(queue => queue.QueueName).ToArray()
        };
    }
}