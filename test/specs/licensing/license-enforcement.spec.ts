import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { expiredLicenseMessageWithValue } from "./questions/expiredLicenseMessageWithValue";
import { viewYourLicenseButton } from "./questions/viewYourLicenseButton";
import { extendYourLicenseButton } from "./questions/extendYourLicenseButton";
import { getAlertNotifications } from "./questions/alertNotifications";

describe("FEATURE: EXPIRING license detection", () => {
  describe("RULE: The user should be alerted while using the monitoring endpoint list functionality about an EXPIRING license", () => {
    [{ licenseExtensionUrl: "https://particular.net/extend-your-trial?p=servicepulse" }, { licenseExtensionUrl: "http://custom-url?with-parts=value1" }].forEach(({ licenseExtensionUrl }) => {
      test(`EXAMPLE: Expiring trial with ${licenseExtensionUrl} as license extension url `, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(precondition.LicenseType.Trial, licenseExtensionUrl));

        await driver.goTo("monitoring");

        const notification = (await getAlertNotifications()).find((n) => n.textMatches(/your non-production development license will expire soon\. to continue using the particular service platform you'll need to extend your license\./i));

        expect(notification).not.toBeUndefined();
        expect(notification?.hasLink({ caption: "Extend your license", address: licenseExtensionUrl })).toBeTruthy();
        expect(notification?.hasLink({ caption: "View license details", address: "#/configuration" })).toBeTruthy();
      });
    });

    [
      { description: "Expiring upgrade protection", licenseType: precondition.LicenseType.UpgradeProtection, textMatch: /once upgrade protection expires, you'll no longer have access to support or new product versions/i },
      { description: "Expiring platform subscription", licenseType: precondition.LicenseType.Subscription, textMatch: /Once the license expires you'll no longer be able to continue using the Particular Service Platform/i },
    ].forEach(({ description, licenseType, textMatch }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(licenseType));

        await driver.goTo("monitoring");

        const notification = (await getAlertNotifications()).find((n) => n.textMatches(textMatch));

        expect(notification).not.toBeUndefined();
        expect(notification?.hasLink({ caption: "View license details", address: "#/configuration" })).toBeTruthy();
      });
    });
  });
});
//Once the license expires you'll no longer be able to continue using the Particular Service Platform.
describe("FEATURE: EXPIRED license detection", () => {
  //As of the moment of writing this test, license check is performed during the first load of the application only. No continuous check is performed.
  describe("RULE: Access to the monitoring endpoint list functionality should be blocked when a expired license is detected and a notification should be displayed", () => {
    test("EXAMPLE: Expired trial", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiredLicense(precondition.LicenseType.Trial));

      //Act
      await driver.goTo("monitoring");

      expect(await expiredLicenseMessageWithValue(/to continue using the particular service platform, please extend your license/i)).toBeTruthy();
      expect((await extendYourLicenseButton()).address).toBe("https://particular.net/extend-your-trial?p=servicepulse");
      expect((await viewYourLicenseButton()).address).toBe("#/configuration/license");

      //Find all the toast notifications that popped up and check if there is a notification about the expired license with a link to the expected page
      const notification = (await getAlertNotifications()).find((n) => n.textMatches(/your license has expired\. please contact particular software support at:/i));

      expect(notification).not.toBeUndefined();
      expect(notification?.hasLink({ caption: "http://particular.net/support", address: "http://particular.net/support" })).toBeTruthy();
    });

    test("EXAMPLE: Expired platform subscription", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiredLicense(precondition.LicenseType.Subscription));

      //Act
      await driver.goTo("monitoring");

      //expect(await screen.findByText(/please update your license to continue using the particular service platform/i)).toBeInTheDocument();
      expect(await expiredLicenseMessageWithValue(/please update your license to continue using the particular service platform/i)).toBeTruthy();
      expect((await viewYourLicenseButton()).address).toBe("#/configuration/license");

      //Find all the toast notifications that popped up and check if there is a notification about the expired license with a link to the expected page
      const notification = (await getAlertNotifications()).find((n) => n.textMatches(/your license has expired\. please contact particular software support at:/i));

      expect(notification).not.toBeUndefined();
      expect(notification?.hasLink({ caption: "http://particular.net/support", address: "http://particular.net/support" })).toBeTruthy();
    });

    test("EXAMPLE: Expired upgrade protection", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiredLicense(precondition.LicenseType.UpgradeProtection));

      //Act
      await driver.goTo("monitoring");

      expect(await expiredLicenseMessageWithValue(/your upgrade protection period has elapsed and your license is not valid for this version of servicepulse\./i)).toBeTruthy();
      expect((await viewYourLicenseButton()).address).toBe("#/configuration/license");

      //Find all the toast notifications that popped up and check if there is a notification about the expired license with a link to the expected page
      const notification = (await getAlertNotifications()).find((n) => n.textMatches(/your license has expired\. please contact particular software support at:/i));

      expect(notification).not.toBeUndefined();
      expect(notification?.hasLink({ caption: "http://particular.net/support", address: "http://particular.net/support" })).toBeTruthy();
    });
  });
});
