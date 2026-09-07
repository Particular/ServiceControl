namespace ServiceControl.Operations
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
        public CheckRemotes(Settings settings, IHttpClientFactory httpClientFactory) : this(settings, httpClientFactory, TimeSpan.FromSeconds(10))
        {
        }

        internal CheckRemotes(Settings settings, IHttpClientFactory httpClientFactory, TimeSpan probeTimeout) : base("ServiceControl Remotes", "Health", TimeSpan.FromSeconds(30))
        {
            this.httpClientFactory = httpClientFactory;
            this.probeTimeout = probeTimeout;
            remoteInstanceSetting = settings.RemoteInstances;
            remoteQueryTasks = new List<Task>(remoteInstanceSetting.Length);
        }

        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            try
            {
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                probe.CancelAfter(probeTimeout);

                foreach (var remote in remoteInstanceSetting)
                {
                    remoteQueryTasks.Add(CheckSuccessStatusCode(remote, probe, cancellationToken));
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

        async Task CheckSuccessStatusCode(RemoteInstanceSetting remoteSettings, CancellationTokenSource probe, CancellationToken cancellationToken)
        {
            try
            {
                var client = httpClientFactory.CreateClient(remoteSettings.InstanceId);
                // The remote's client is configured with the query time limit; the probe runs on its own budget.
                client.Timeout = Timeout.InfiniteTimeSpan;

                var response = await client.GetAsync("/api", probe.Token);
                response.EnsureSuccessStatusCode();
                remoteSettings.TemporarilyUnavailable = false;
            }
            catch (HttpRequestException e)
            {
                remoteSettings.TemporarilyUnavailable = true;
                throw new TimeoutException($"The remote instance at '{remoteSettings.BaseAddress}' doesn't seem to be available. It will be temporarily disabled. Reason: {e.Message}", e);
            }
            catch (OperationCanceledException e) when (probe.Token.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Shutting down; an aborted probe says nothing about the remote's health
                    return;
                }

                remoteSettings.TemporarilyUnavailable = true;
                throw new TimeoutException($"The remote at '{remoteSettings.BaseAddress}' did not respond within the allotted time of '{probeTimeout}'. It will be temporarily disabled.", e);
            }
        }

        readonly IHttpClientFactory httpClientFactory;
        readonly TimeSpan probeTimeout;
        RemoteInstanceSetting[] remoteInstanceSetting;
        List<Task> remoteQueryTasks;
    }
}