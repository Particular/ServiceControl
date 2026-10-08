namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;

    public enum WebhookAlertType
    {
        MessageFailed,
        HeartbeatStopped,
        CustomCheckFailed
    }

    /// <summary>
    /// A configured webhook target. Contains secrets (URL, headers), so it must never be logged or serialized as a whole.
    /// </summary>
    public class WebhookTarget
    {
        public required string Name { get; init; }

        public required Uri Url { get; init; }

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// Optional JUST.net transformation template applied to the default Alertmanager payload.
        /// </summary>
        public string Template { get; init; }

        /// <summary>
        /// Set when <see cref="Template"/> is present but cannot be used. Deliveries to this webhook fail until the template is fixed.
        /// </summary>
        public string TemplateError { get; init; }

        /// <summary>
        /// The alert types sent to this webhook. Null means all alert types.
        /// </summary>
        public IReadOnlySet<WebhookAlertType> Alerts { get; init; }

        public bool HasTemplate => Template != null;

        public bool Accepts(WebhookAlertType alertType) => Alerts == null || Alerts.Contains(alertType);

        public override string ToString() => Name;
    }
}
