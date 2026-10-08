namespace ServiceControl.Notifications.Webhooks
{
    using System;

    class WebhookDeliveryOptions
    {
        /// <summary>
        /// Retries after the initial attempt for transient failures (network errors, timeouts, HTTP 408, 429, and 5xx).
        /// </summary>
        public int MaxRetryAttempts { get; init; } = 3;

        public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Upper bound for the exponential backoff and for delays requested by a Retry-After response header.
        /// </summary>
        public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(15);

        public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Maximum number of alerts sent in a single default (untransformed) payload. Templated webhooks always receive one alert per request.
        /// </summary>
        public int MaxAlertsPerNotification { get; init; } = 25;
    }
}
