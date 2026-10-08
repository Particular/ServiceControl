namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Polly.Retry;
    using Polly.Timeout;
    using ServiceBus.Management.Infrastructure.Settings;

    class WebhookSender
    {
        public const string HttpClientName = "ServiceControl.Webhooks";

        public WebhookSender(Settings settings, IHttpClientFactory httpClientFactory, WebhookDeliveryOptions options, IDomainEvents domainEvents, TimeProvider timeProvider, ILogger<WebhookSender> logger)
        {
            this.settings = settings;
            this.httpClientFactory = httpClientFactory;
            this.domainEvents = domainEvents;
            this.timeProvider = timeProvider;
            this.logger = logger;
            pipeline = CreatePipeline(options);
        }

        public async Task Send(string webhookName, IReadOnlyCollection<AlertmanagerAlert> alerts, CancellationToken cancellationToken = default)
        {
            var webhook = settings.Webhooks.FirstOrDefault(w => string.Equals(w.Name, webhookName, StringComparison.OrdinalIgnoreCase));
            if (webhook == null)
            {
                logger.LogWarning("Dropping notification for webhook {WebhookName} because it is no longer configured", webhookName);
                return;
            }

            if (alerts.Count == 0)
            {
                return;
            }

            string payload;
            try
            {
                payload = BuildPayload(webhook, alerts);
            }
            catch (WebhookTemplateException e)
            {
                await ReportFailure(webhook, alerts, $"The payload template could not be applied. {e.Message}", null, e, cancellationToken);
                return;
            }

            // The pipeline passes its own token to each attempt, which is linked to cancellationToken and additionally enforces the attempt timeout
#pragma warning disable PS0021
            try
            {
                var client = httpClientFactory.CreateClient(HttpClientName);

                using var response = await pipeline.ExecuteAsync(async (token) =>
                {
                    using var request = CreateRequest(webhook, payload);
                    return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                }, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Delivered {AlertCount} alert(s) to webhook {WebhookName}", alerts.Count, webhook.Name);
                    return;
                }

                var responseBody = await ReadResponseSnippet(response, cancellationToken);
                await ReportFailure(webhook, alerts, $"The webhook responded with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). {responseBody}".TrimEnd(), response.StatusCode, null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                var reason = e is TimeoutRejectedException
                    ? "The webhook did not respond in time."
                    : $"The request failed. {e.GetBaseException().Message}";
                await ReportFailure(webhook, alerts, reason, null, e, cancellationToken);
            }
#pragma warning restore PS0021
        }

        internal static string BuildPayload(WebhookTarget webhook, IReadOnlyCollection<AlertmanagerAlert> alerts)
        {
            var payload = AlertmanagerPayload.Serialize(alerts);

            if (!webhook.HasTemplate)
            {
                return payload;
            }

            if (webhook.TemplateError != null)
            {
                throw new WebhookTemplateException(webhook.TemplateError);
            }

            return JustTemplateTransformer.Transform(webhook.Template, payload);
        }

        static HttpRequestMessage CreateRequest(WebhookTarget webhook, string payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            foreach (var (name, value) in webhook.Headers)
            {
                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    // Content headers such as Content-Type can't be set on the request itself
                    request.Content.Headers.Remove(name);
                    request.Content.Headers.TryAddWithoutValidation(name, value);
                }
            }

            if (!request.Headers.Contains("User-Agent"))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            }

            return request;
        }

        ResiliencePipeline<HttpResponseMessage> CreatePipeline(WebhookDeliveryOptions options) =>
            new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = timeProvider }
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = options.RetryDelay,
                    MaxDelay = options.MaxRetryDelay,
                    ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
                    DelayGenerator = args => ValueTask.FromResult(GetRetryAfter(args.Outcome.Result, timeProvider.GetUtcNow(), options.MaxRetryDelay)),
                    OnRetry = args =>
                    {
                        logger.LogWarning(args.Outcome.Exception, "Webhook delivery attempt {Attempt} failed with {Outcome}. Retrying in {RetryDelay}",
                            args.AttemptNumber + 1,
                            args.Outcome.Result != null ? $"HTTP {(int)args.Outcome.Result.StatusCode}" : args.Outcome.Exception?.GetType().Name,
                            args.RetryDelay);
                        return default;
                    }
                })
                // Added after the retry strategy so the timeout applies to each attempt individually
                .AddTimeout(options.AttemptTimeout)
                .Build();

        internal static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
            outcome.Exception switch
            {
                HttpRequestException or TimeoutRejectedException => true,
                null => outcome.Result.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)outcome.Result.StatusCode >= 500,
                _ => false
            };

        internal static TimeSpan? GetRetryAfter(HttpResponseMessage response, DateTimeOffset now, TimeSpan maxDelay)
        {
            var retryAfter = response?.Headers.RetryAfter;
            var delay = retryAfter?.Delta ?? (retryAfter?.Date - now);

            if (delay is null || delay <= TimeSpan.Zero)
            {
                return null;
            }

            return delay > maxDelay ? maxDelay : delay;
        }

        static async Task<string> ReadResponseSnippet(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return body.Length > MaxResponseSnippetLength ? body[..MaxResponseSnippetLength] + "…" : body;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        async Task ReportFailure(WebhookTarget webhook, IReadOnlyCollection<AlertmanagerAlert> alerts, string reason, HttpStatusCode? statusCode, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(exception,
                "Webhook notification {WebhookName} failed with status {StatusCode}: {Reason}. Alerts: {AlertNames}, dedup keys: {DedupKeys}",
                webhook.Name,
                statusCode.HasValue ? (int)statusCode.Value : null,
                reason,
                string.Join(", ", alerts.Select(a => a.IsResolved ? $"{a.AlertName} (resolved)" : a.AlertName)),
                string.Join(", ", alerts.Select(a => a.Annotations.GetValueOrDefault(AlertAnnotations.DedupKey))));

            try
            {
                await domainEvents.Raise(new WebhookNotificationFailed
                {
                    WebhookName = webhook.Name,
                    AlertCount = alerts.Count,
                    Reason = reason
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                // Failing the message would only redeliver the notification, which already failed
                logger.LogWarning(e, "Failed to record the failure of webhook notification {WebhookName}", webhook.Name);
            }
        }

        readonly Settings settings;
        readonly IHttpClientFactory httpClientFactory;
        readonly IDomainEvents domainEvents;
        readonly TimeProvider timeProvider;
        readonly ILogger<WebhookSender> logger;
        readonly ResiliencePipeline<HttpResponseMessage> pipeline;

        static readonly string UserAgent = $"ServiceControl/{ServiceControlVersion.GetFileVersion()}";
        const int MaxResponseSnippetLength = 512;
    }
}
