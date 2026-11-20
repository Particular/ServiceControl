namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Notifications;
using ServiceControl.Persistence.DataMigration;

class SettingsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly string[] SettingsCategoryIds = [MigrationCategoryIds.LicensingReportMasks, MigrationCategoryIds.LicensedEndpointDetails, MigrationCategoryIds.NotificationSettings, MigrationCategoryIds.TrialEndDate];

    [TestCase(MigrationCategoryIds.LicensingReportMasks)]
    [TestCase(MigrationCategoryIds.LicensedEndpointDetails)]
    [TestCase(MigrationCategoryIds.NotificationSettings)]
    [TestCase(MigrationCategoryIds.TrialEndDate)]
    public async Task Count_is_the_row_under_the_categorys_own_key_alone(string categoryId)
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == categoryId);

        foreach (var otherCategoryId in SettingsCategoryIds.Where(id => id != categoryId))
        {
            await StoreSettingFor(otherCategoryId);
        }

        Assert.That(await Target.Count(category), Is.Zero, "the other settings rows are not rows of this category");

        await StoreSettingFor(categoryId);

        Assert.That(await Target.Count(category), Is.EqualTo(1), "counted as the whole Settings table, this would also count the other settings rows");
    }

    Task StoreSettingFor(string categoryId) => categoryId switch
    {
        MigrationCategoryIds.LicensingReportMasks => LicensingDataStore.SaveReportMasks(["contoso"]),
        MigrationCategoryIds.LicensedEndpointDetails => LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails { LicenseId = "abc-123" }),
        MigrationCategoryIds.NotificationSettings => NotificationsStore.SaveSettings(new NotificationsSettings { Email = new EmailNotifications { SmtpServer = "smtp.contoso.com" } }),
        MigrationCategoryIds.TrialEndDate => ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>().StoreTrialEndDate(new DateOnly(2027, 3, 15)),
        _ => throw new ArgumentOutOfRangeException(nameof(categoryId), categoryId, "This test stores no settings row for that category.")
    };
}
