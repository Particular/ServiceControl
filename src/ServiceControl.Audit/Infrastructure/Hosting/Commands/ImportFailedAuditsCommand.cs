﻿namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NLog;
    using NServiceBus;
    using Settings;

    class ImportFailedAuditsCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            settings.IngestAuditMessages = false;

            var endpointConfiguration = new EndpointConfiguration(settings.ServiceName);
            var loggingSettings = new LoggingSettings(settings.ServiceName, LogLevel.Info);

            using var tokenSource = new CancellationTokenSource();

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.AddServiceControlAudit((_, __) =>
            {
                tokenSource.Cancel();
                return Task.CompletedTask;
            }, settings, endpointConfiguration, loggingSettings);

            using var app = hostBuilder.Build();
            await app.StartAsync(tokenSource.Token);

            var importer = app.Services.GetRequiredService<ImportFailedAudits>();

            Console.CancelKeyPress += (_, _) => { tokenSource.Cancel(); };

            try
            {
                await importer.Run(tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // no op
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }
    }
}