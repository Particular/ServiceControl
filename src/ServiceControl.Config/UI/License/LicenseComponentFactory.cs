namespace ServiceControl.Config.UI.License
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using ServiceControlInstaller.Engine.LicenseMgmt;

    class LicenseComponentFactory
    {
        readonly DateTime today;
        const int ExpiryWarningPeriodInDays = 10;

        public LicenseComponentFactory(DateTime? today = null)
        {
            this.today = today ?? DateTime.Today;
        }

        public IEnumerable<LicenseComponent> CreateComponents(LicenseDetails details)
        {
            if (details.IsTrialLicense)
            {
                yield return new LicenseComponent
                {
                    Label = "Platform license type:",
                    Value = "Trial"
                };

                if (details.ExpirationDate.HasValue)
                {
                    yield return TrialExpiryComponent(details.ExpirationDate.Value);
                }
            }
            else
            {
                yield return new LicenseComponent
                {
                    Label = "Platform license type:",
                    Value = $"{details.LicenseType}, {details.Edition}"
                };

                if (details.ExpirationDate.HasValue)
                {
                    yield return PlatformExpiryComponent(details.ExpirationDate.Value);
                }

                if (details.UpgradeProtectionExpiration.HasValue)
                {
                    yield return UpgradeProtectionExpiryComponent(details.UpgradeProtectionExpiration.Value);
                }
            }
        }

        LicenseComponent TrialExpiryComponent(DateTime expiryDate)
        {
            var component = new LicenseComponent {Label = "Trial expiry date:"};

            var value = new StringBuilder(expiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(expiryDate - today).TotalDays;

            if (daysUntilExpiry < 0)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} ago");
                component.Importance = Importance.Serious;
                component.ShortText = "Trial expired";
                component.WarningText = "Your trial has expired. To continue using the Particular Service Platform you'll need to extend your trial or purchase a license.";
            }
            else if (daysUntilExpiry < ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} left");
                component.Importance = Importance.Warning;
                component.ShortText = $"Warning: Trial expiring in {Inflect(daysUntilExpiry, "day", "days")}";
                component.WarningText = "Your trial will expire soon. To continue using the Particular Service Platform you'll need to extend your trial or purchase a license.";
            }

            component.Value = value.ToString();

            return component;
        }


        LicenseComponent PlatformExpiryComponent(DateTime expiryDate)
        {
            var component = new LicenseComponent {Label = "Platform license expiry date:"};

            var value = new StringBuilder(expiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(expiryDate - today).TotalDays;

            if (daysUntilExpiry < 0)
            {
                value.AppendFormat($" - {Inflect(-daysUntilExpiry, "day", "days")} ago");
                component.Importance = Importance.Serious;
                component.ShortText = "Platform license expired";
                component.WarningText = "Your platform license has expired. Please update your license to continue using the Particular Service Platform.";
            }
            else if (daysUntilExpiry < ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} left");
                component.Importance = Importance.Serious;
                component.ShortText = $"Warning: Platform license expiring in {Inflect(daysUntilExpiry, "day", "days")}";
                component.WarningText = "Once the license expires you'll no longer be able to continue using the Particular Service Platform.";
            }

            component.Value = value.ToString();

            return component;
        }

        LicenseComponent UpgradeProtectionExpiryComponent(DateTime upgradeProtectionExpiryDate)
        {
            var component = new LicenseComponent {Label = "Upgrade protection expiry date:"};

            var value = new StringBuilder(upgradeProtectionExpiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(upgradeProtectionExpiryDate - today).TotalDays;

            if (daysUntilExpiry < 0)
            {
                value.AppendFormat($" - {Inflect(-daysUntilExpiry, "day", "days")} ago");
                component.Importance = Importance.Warning;
                component.ShortText = "Platform license expired";
                component.WarningText = "Once upgrade protection expires, you'll no longer have access to support or new product versions.";
            }
            else if (daysUntilExpiry < ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} left");
                component.Importance = Importance.Warning;
                component.ShortText = $"Warning: Upgrade protection expiring in {Inflect(daysUntilExpiry, "day", "days")}";
                component.WarningText = "Once upgrade protection expires, you'll no longer have access to support or new product versions.";
            }

            component.Value = value.ToString();

            return component;

        }

        static string Inflect(int count, string singular, string plural) => count == 1
            ? $"{count} {singular}"
            : $"{count} {plural}";
    }
}