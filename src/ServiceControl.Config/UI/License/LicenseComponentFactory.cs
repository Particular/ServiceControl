namespace ServiceControl.Config.UI.License
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.LicenseManagement;

    static class LicenseComponentFactory
    {
        const string SubscriptionLicenseText = "Please extend your license to continue using the Particular Service Platform.";

        static readonly CountInflector DaysInflector = new()
        {
            Singular = "{0} day",
            Plural = "{0} days"
        };

        public static IEnumerable<LicenseComponent> CreateComponents(LicenseDetails details)
        {
            yield return new LicenseComponent
            {
                Label = "Platform license type:",
                Value = string.Join(", ", new[]
                {
                    details.LicenseType,
                    details.Edition
                }.Where(x => x != null))
            };

            if (details.ExpirationDate.HasValue)
            {
                yield return SubscriptionExpiryComponent(details);
            }

            if (details.UpgradeProtectionExpiration.HasValue)
            {
                yield return UpgradeProtectionExpiryComponent(details);
            }
        }

        static LicenseComponent SubscriptionExpiryComponent(LicenseDetails details)
        {
            if (details.WarnUserSubscriptionHasExpired || details.WarnUserTrialHasExpired)
            {
                return new LicenseComponent
                {
                    Label = "Platform license expiry date:",
                    Value = $"{details.ExpirationDate:d} - expired",
                    Importance = Importance.Serious,
                    ShortText = "Platform license expired",
                    WarningText = SubscriptionLicenseText
                };
            }

            if (details.WarnUserSubscriptionIsExpiring || details.WarnUserTrialIsExpiring)
            {
                var daysRemain = DaysInflector.Inflect(details.DaysUntilSubscriptionExpires ?? 0);
                return new LicenseComponent
                {
                    Label = "Platform license expiry date:",
                    Value = $"{details.ExpirationDate:d} - {daysRemain} left",
                    Importance = details.WarnUserTrialIsExpiring ? Importance.Warning : Importance.Serious,
                    ShortText = $"Platform license expiring in {daysRemain}",
                    WarningText = SubscriptionLicenseText
                };
            }

            return new LicenseComponent
            {
                Label = "Platform license expiry date:",
                Value = $"{details.ExpirationDate:d}"
            };
        }

        static LicenseComponent UpgradeProtectionExpiryComponent(LicenseDetails details)
        {
            return new LicenseComponent
            {
                Label = "Upgrade protection expiry date:",
                Value = $"{details.UpgradeProtectionExpiration:d}"
            };
        }
    }
}