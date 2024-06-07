using System.CommandLine;

Exceptions.SetupUnhandledExceptionHandling();

try
{
    var rootCommand = new RootCommand("A tool to measure NServiceBus endpoints and throughput.");

    //rootCommand.AddCommand(ServiceControlCommand.CreateCommand());
    //rootCommand.AddCommand(AzureServiceBusCommand.CreateCommand());
    //rootCommand.AddCommand(RabbitMqCommand.CreateCommand());
    rootCommand.AddCommand(SqlServerCommand.CreateCommand());
    //rootCommand.AddCommand(SqsCommand.CreateCommand());

    SharedOptions.Register(rootCommand);

    var returnCode = await rootCommand.InvokeAsync(args);
    return returnCode;
}
catch (Exception x)
{
    Out.WriteError(w =>
    {
        w.WriteLine(x);
        w.WriteLine();
        w.WriteLine("Unable to execute command, please contact Particular Software support.");
    });

    return (int)HaltReason.RuntimeError;
}