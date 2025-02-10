﻿namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckRemotes : CustomCheck
    {
        public CheckRemotes(Settings settings, IHttpClientFactory httpClientFactory) : base("ServiceControl Remotes", "Health", TimeSpan.FromSeconds(30))
        {
            this.httpClientFactory = httpClientFactory;
            remoteInstanceSetting = settings.RemoteInstances;
            remoteQueryTasks = new List<Task>(remoteInstanceSetting.Length);
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            try
            {
                var queryTimeout = TimeSpan.FromSeconds(10);
                using var cancellationTokenSource = new CancellationTokenSource(queryTimeout);
                foreach (var remote in remoteInstanceSetting)
                {
                    remoteQueryTasks.Add(CheckSuccessStatusCode(httpClientFactory, remote, queryTimeout, cancellationTokenSource.Token));
                }

                try
                {
                    await Task.WhenAll(remoteQueryTasks);
                    return CheckResult.Pass;
                }
                catch (Exception)
                {
                    var builder = new StringBuilder();

                    foreach (var task in remoteQueryTasks)
                    {
                        try
                        {
                            await task;
                        }
                        catch (TimeoutException e)
                        {
                            builder.AppendLine(e.Message);
                        }
                    }

                    return CheckResult.Failed(builder.ToString());
                }
            }
            finally
            {
                remoteQueryTasks.Clear();
            }
        }

        static async Task CheckSuccessStatusCode(IHttpClientFactory httpClientFactory, RemoteInstanceSetting remoteSettings, TimeSpan queryTimeout, CancellationToken cancellationToken)
        {
            try
            {
                var client = httpClientFactory.CreateClient(remoteSettings.InstanceId);
                var response = await client.GetAsync("/api", cancellationToken);
                response.EnsureSuccessStatusCode();
                remoteSettings.TemporarilyUnavailable = false;
            }
            catch (HttpRequestException e)
            {
                remoteSettings.TemporarilyUnavailable = true;
                throw new TimeoutException($"The remote instance at '{remoteSettings.BaseAddress}' doesn't seem to be available. It will be temporarily disabled. Reason: {e.Message}", e);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancelled, noop
            }
            catch (OperationCanceledException e) // Intentional, OCE gracefully handled by other catch
            {
                remoteSettings.TemporarilyUnavailable = true;
                throw new TimeoutException($"The remote at '{remoteSettings.BaseAddress}' did not respond within the allotted time of '{queryTimeout}'. It will be temporarily disabled.", e);
            }
        }

        readonly IHttpClientFactory httpClientFactory;
        RemoteInstanceSetting[] remoteInstanceSetting;
        List<Task> remoteQueryTasks;
    }
}