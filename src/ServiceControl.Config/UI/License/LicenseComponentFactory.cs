namespace ServiceControl.Config.UI.License
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    class LicenseComponentFactory
    {
        const string TrialLicenseText = "Please extend your trial or purchase a license to continue using the Particular Service Platform.";
        const string UpgradeProtectionLicenseText = "Please extend your upgrade protection so that we can continue to provide you with support and new versions of the Particular Service Platform.";
        const string SubscriptionLicenseText = "Please extend your license to continue using the Particular Service Platform.";

        CountInflector daysInflector = new CountInflector
        {
            Singular = "{0} day",
            Plural = "{0} days"
        };

        public IEnumerable<LicenseComponent> CreateComponents(LicenseDetails details)
        {
            return details.IsTrialLicense ? TrialLicense(details) : CommercialLicense(details);
        }

        IEnumerable<LicenseComponent> TrialLicense(LicenseDetails details)
        {
            yield return new LicenseComponent
            {
                Label = "Platform license type:",
                Value = "Trial"
            };

            yield return TrialExpiryComponent(details);
        }

        LicenseComponent TrialExpiryComponent(LicenseDetails details)
        {
            if (details.WarnUserTrialHasExpired)
            {
                return new LicenseComponent
                {
                    Label = "Trial expiry date:",
                    Value = $"{details.ExpirationDate:d} - expired",
                    Importance = Importance.Serious,
                    ShortText = "Trial expired",
                    WarningText = TrialLicenseText
                };
            }

            if (details.WarnUserTrialIsExpiring)
            {
                var daysRemaining = daysInflector.Inflect(details.DaysUntilSubscriptionExpires ?? 0);
                return new LicenseComponent
                {
                    Label = "Trial expiry date:",
                    Value = $"{details.ExpirationDate:d} - {daysRemaining} left",
                    Importance = Importance.Warning,
                    ShortText = $"Warning: Trial expiring in {daysRemaining}",
                    WarningText = TrialLicenseText
                };
            }

            return new LicenseComponent
            {
                Label = "Trial expiry date:",
                Value = $"{details.ExpirationDate:d}"
            };
        }

        IEnumerable<LicenseComponent> CommercialLicense(LicenseDetails details)
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

        LicenseComponent SubscriptionExpiryComponent(LicenseDetails details)
        {
            if (details.WarnUserSubscriptionHasExpired)
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

            if (details.WarnUserSubscriptionIsExpiring)
            {
                var daysRemain = daysInflector.Inflect(details.DaysUntilSubscriptionExpires ?? 0);
                return new LicenseComponent
                {
                    Label = "Platform license expiry date:",
                    Value = $"{details.ExpirationDate:d} - {daysRemain} left",
                    Importance = Importance.Serious,
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

        LicenseComponent UpgradeProtectionExpiryComponent(LicenseDetails details)
        {
            if (details.WarnUserUpgradeProtectionHasExpired)
            {
                return new LicenseComponent
                {
                    Label = "Upgrade protection expiry date:",
                    Value = $"{details.UpgradeProtectionExpiration:d} - expired",
                    Importance = Importance.Warning,
                    ShortText = "Platform license expired",
                    WarningText = UpgradeProtectionLicenseText
                };
            }

            if (details.WarnUserUpgradeProtectionIsExpiring)
            {
                var daysRemain = daysInflector.Inflect(details.DaysUntilUpgradeProtectionExpires ?? 0);
                return new LicenseComponent
                {
                    Label = "Upgrade protection expiry date:",
                    Value = $"{details.UpgradeProtectionExpiration:d} - {daysRemain} left",
                    Importance = Importance.Warning,
                    ShortText = $"Warning: Upgrade protection expiring in {daysRemain}",
                    WarningText = UpgradeProtectionLicenseText
                };
            }

            return new LicenseComponent
            {
                Label = "Upgrade protection expiry date:",
                Value = $"{details.UpgradeProtectionExpiration:d}"
            };
        }
    }
}