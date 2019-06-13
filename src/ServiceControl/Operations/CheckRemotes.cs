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
        public CheckRemotes(Settings settings, Func<HttpClient> httpClientFactory) : base("ServiceControl Remotes", "Health", TimeSpan.FromSeconds(30))
        {
            this.httpClientFactory = httpClientFactory;
            remoteInstanceSetting = settings.RemoteInstances;
            remoteQueryTasks = new List<Task>(remoteInstanceSetting.Length);
        }

        public override async Task<CheckResult> PerformCheck()
        {
            var httpClient = httpClientFactory();

            try
            {
                var queryTimeout = TimeSpan.FromSeconds(10);
                using (var cancellationTokenSource = new CancellationTokenSource(queryTimeout))
                {
                    foreach (var remote in remoteInstanceSetting)
                    {
                        remoteQueryTasks.Add(CheckSuccessStatusCode(httpClient, remote, queryTimeout, cancellationTokenSource.Token));
                    }

                    try
                    {
                        await Task.WhenAll(remoteQueryTasks).ConfigureAwait(false);
                        return CheckResult.Pass;
                    }
                    catch (Exception)
                    {
                        var builder = new StringBuilder();

                        foreach (var task in remoteQueryTasks)
                        {
                            try
                            {
                                await task.ConfigureAwait(false);
                            }
                            catch (TimeoutException e)
                            {
                                builder.AppendLine(e.Message);
                            }
                        }

                        return CheckResult.Failed(builder.ToString());
                    }
                }
            }
            finally
            {
                remoteQueryTasks.Clear();
            }
        }

        static async Task CheckSuccessStatusCode(HttpClient client, RemoteInstanceSetting remoteSettings, TimeSpan queryTimeout, CancellationToken token)
        {
            try
            {
                var response = await client.GetAsync(remoteSettings.ApiUri, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new TimeoutException($"Remote at '{remoteSettings.ApiUri}' doesn't seem to be available. Reason: {e.Message}", e);
            }
            catch (OperationCanceledException e)
            {
                throw new TimeoutException($"Remote at '{remoteSettings.ApiUri}' did not respond within the allotted timespan of '{queryTimeout}'.", e);
            }
        }

        readonly Func<HttpClient> httpClientFactory;
        RemoteInstanceSetting[] remoteInstanceSetting;
        List<Task> remoteQueryTasks;
    }
}