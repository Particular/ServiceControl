namespace ServiceControl.Audit.Rotation
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.CommandLine.Rendering;
    using System.CommandLine.Rendering.Views;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using ServiceControlInstaller.Engine.Instances;

    class PrintCommand : Command
    {
        static readonly ILog Log = LogManager.GetLogger<PrintCommand>();

        public PrintCommand()
            : base("print", "Prints the information about the rotation scheme and state")
        {
            Handler = CommandHandler.Create<InvocationContext>(
                context =>
                {
                    var views = new List<View>();

                    Log.Info("Loading rotation scheme");

                    var schemeJson = File.ReadAllText("rotation-scheme.json");
                    var scheme = JsonConvert.DeserializeObject<RotationScheme>(schemeJson);

                    Log.Info("Loading rotation state");

                    var stateJson = File.ReadAllText("rotation-state.json");
                    var rotationState = JsonConvert.DeserializeObject<RotationState>(stateJson);

                    Log.Info("Detecting installed audit instances");
                    var auditInstances = InstanceFinder.ServiceControlAuditInstances();

                    var instanceModel = scheme.Instances.Select((x, i) =>
                    {
                        var model = new InstanceModel
                        {
                            Name = x,
                            Index = i,
                            IsActive = rotationState.ActiveInstanceIndex == i
                        };

                        var info = auditInstances.FirstOrDefault(y => y.Name == x);
                        if (info == null)
                        {
                            model.IsInstalled = false;
                        }
                        else
                        {
                            model.IsInstalled = true;
                            model.DatabaseSizeMB = (int)(info.GetDatabaseSizeInGb() * 1000);
                        }

                        return model;
                    }).ToList();

                    views.Add(new ContentView(new ContainerSpan("Timer trigger".Bold(), new ContentSpan(": " + FormatTimerTrigger(scheme)))));
                    views.Add(new ContentView(new ContainerSpan("Database size trigger".Bold(), new ContentSpan(": " + FormatSizeTrigger(scheme)))));
                    views.Add(new ContentView(Environment.NewLine));
                    views.Add(new ContentView("Instances".Bold()));

                    var table = new TableView<InstanceModel>
                    {
                        Items = instanceModel,
                    };
                    table.AddColumn(i => i.Index, new ContentView("Id".Underline()), ColumnDefinition.Fixed(4));
                    table.AddColumn(i => i.Name, new ContentView("Name".Underline()), ColumnDefinition.Fixed(40));
                    table.AddColumn(i => i.IsInstalled, new ContentView("Installed".Underline()), ColumnDefinition.Fixed(10));
                    table.AddColumn(i => i.IsActive, new ContentView("Active".Underline()), ColumnDefinition.Fixed(7));
                    table.AddColumn(i => i.DatabaseSizeMB, new ContentView("DB size (MB)".Underline()), ColumnDefinition.Star(1));
                    views.Add(table);

                    var console = context.Console;
                    if (console is ITerminal terminal)
                    {
                        terminal.Clear();
                    }
                    var stack = new StackLayoutView(Orientation.Vertical);
                    foreach (var view in views)
                    {
                        stack.Add(view);
                    }

                    console.Append(stack);
                });
        }

        static string FormatTimerTrigger(RotationScheme scheme) => scheme.TimerTrigger.HasValue ? scheme.TimerTrigger.Value.ToString() : "none";
        static string FormatSizeTrigger(RotationScheme scheme) => scheme.SizeTriggerMB.HasValue ? scheme.SizeTriggerMB.Value.ToString() + " MB" : "none";

        class InstanceModel
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public bool IsInstalled { get; set; }
            public bool IsActive { get; set; }
            public int DatabaseSizeMB { get; set; }
        }
    }
}