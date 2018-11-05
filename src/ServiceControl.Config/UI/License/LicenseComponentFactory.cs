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

        const string TrialLicenseText = "Please extend your trial or purchase a license to continue using the Particular Service Platform.";
        const string UpgradeProtectionLicenseText = "Please extend your upgrade protection so that we can continue to provide you with support and new versions of the Particular Service Platform.";
        const string SubscriptionLicenseText = "Please extend your license to continue using the Particular Service Platform.";

        CountInflector daysInflector = new CountInflector
        {
            Singular = "{0} day",
            Plural = "{0} days"
        };

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
                value.Append(" - expired");
                component.Importance = Importance.Serious;
                component.ShortText = "Trial expired";
                component.WarningText = TrialLicenseText;
            }
            else if (daysUntilExpiry <= ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {daysInflector.Inflect(daysUntilExpiry)} left");
                component.Importance = Importance.Warning;
                component.ShortText = $"Warning: Trial expiring in {daysInflector.Inflect(daysUntilExpiry)}";
                component.WarningText = TrialLicenseText;
            }

            component.Value = value.ToString();

            return component;
        }


        LicenseComponent PlatformExpiryComponent(DateTime expiryDate)
        {
            var component = new LicenseComponent {Label = "Platform license expiry date:"};

            var value = new StringBuilder(expiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(expiryDate - today).TotalDays + 1;

            if (daysUntilExpiry < 0)
            {
                value.Append(" - expired");
                component.Importance = Importance.Serious;
                component.ShortText = "Platform license expired";
                component.WarningText = SubscriptionLicenseText;
            }
            else if (daysUntilExpiry <= ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {daysInflector.Inflect(daysUntilExpiry)} left");
                component.Importance = Importance.Serious;
                component.ShortText = $"Warning: Platform license expiring in {daysInflector.Inflect(daysUntilExpiry)}";
                component.WarningText = SubscriptionLicenseText;
            }

            component.Value = value.ToString();

            return component;
        }

        LicenseComponent UpgradeProtectionExpiryComponent(DateTime upgradeProtectionExpiryDate)
        {
            var component = new LicenseComponent {Label = "Upgrade protection expiry date:"};

            var value = new StringBuilder(upgradeProtectionExpiryDate.ToShortDateString());

            var daysUntilExpiry = (int)(upgradeProtectionExpiryDate - today).TotalDays + 1;

            if (daysUntilExpiry < 0)
            {
                value.Append(" - expired");
                component.Importance = Importance.Warning;
                component.ShortText = "Platform license expired";
                component.WarningText = UpgradeProtectionLicenseText;
            }
            else if (daysUntilExpiry <= ExpiryWarningPeriodInDays)
            {
                value.AppendFormat($" - {daysInflector.Inflect(daysUntilExpiry)} left");
                component.Importance = Importance.Warning;
                component.ShortText = $"Warning: Upgrade protection expiring in {daysInflector.Inflect(daysUntilExpiry)}";
                component.WarningText = UpgradeProtectionLicenseText;
            }

            component.Value = value.ToString();

            return component;
        }
    }
}