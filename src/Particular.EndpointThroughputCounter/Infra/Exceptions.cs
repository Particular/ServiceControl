using Microsoft.Data.SqlClient;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.AspNetCore;
using Particular.EndpointThroughputCounter.Infra;

public static class Exceptions
{
#if RELEASE
    static readonly RaygunClient raygun = new RaygunClient(new RaygunSettings
    {
        ApiKey = "e08ES555Pc1wZUhEQkafEQ"
    });
#endif

    public static void SetupUnhandledExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;

            Out.WriteError(w =>
            {
                // Just to tell the difference between e.Terminating or not based on the log
                var msg = $"An unhandled exception was caught{(e.IsTerminating ? ", forcing a runtime exit" : "")}.";

                Out.WriteLine();
                Out.WriteLine(msg);
                Out.WriteLine(exception.ToString());
                Out.WriteLine();
                Out.WriteLine("Contact Particular Software support for assistance.");
            });

            ReportError(exception);

            Environment.Exit((int)HaltReason.RuntimeError);
        };
    }

    public static void ReportError(Exception x)
    {
        var settings = new RaygunSettings()
        {
            ApplicationVersion = Versioning.NuGetVersion
        };

        RunInfo.Add("ToolOutput", Out.GetToolOutput());

        if (x is SqlException sqlX)
        {
            RunInfo.Add("SqlException.Number", sqlX.Number.ToString());
            if (sqlX.Errors is not null)
            {
                for (var i = 0; i < sqlX.Errors.Count; i++)
                {
                    var err = sqlX.Errors[i];
                    RunInfo.Add($"SqlException.Errors${i}.Number", err.Number.ToString());
                    RunInfo.Add($"SqlException.Errors${i}.Error", err.ToString());
                }
            }
        }

        var message = RaygunMessageBuilder.New(settings)
            .SetExceptionDetails(x)
            .SetEnvironmentDetails()
            .SetMachineName(Environment.MachineName)
            .AddCurrentRunInfo()
            .SetVersion($"{Versioning.NuGetVersion} Sha:{Versioning.FullSha}")
            .Build();

        try
        {
#if DEBUG
            Console.WriteLine(message);
#else
            raygun.Send(message).GetAwaiter().GetResult();
#endif
            Console.WriteLine($"When contacting support, you may reference TicketId: {RunInfo.TicketId}");
        }
        catch (Exception)
        {
            Console.WriteLine("Unable to report tool failure to Particular Software. This may be because outgoing internet access is not available.");
        }

    }
}
