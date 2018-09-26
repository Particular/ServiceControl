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
            yield return new LicenseComponent
            {
                Label = "Platform license type:",
                Value = details.LicenseType
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


        LicenseComponent PlatformExpiryComponent(DateTime expiryDate)
        {
            var component = new LicenseComponent {Label = "Platform license expiry date:"};

            var value = new StringBuilder(expiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(expiryDate - today).TotalDays;

            if (daysUntilExpiry < 0)
            {
                value.AppendFormat($" - {Inflect(-daysUntilExpiry, "day", "days")} ago");
                component.Importance = Importance.Serious;
                component.WarningText = "You are no longer able to continue using the application. Please import a new license or contact us.";
            }
            else if (daysUntilExpiry < ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} left");
                component.Importance = Importance.Serious;
                component.WarningText = "Once the license expires you'll no longer be able to continue using the application.";
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
                component.WarningText = "You'll no longer have access to new product versions. Please import a new license or contact us.";
            }
            else if (daysUntilExpiry < ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {Inflect(daysUntilExpiry, "day", "days")} left");
                component.Importance = Importance.Warning;
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