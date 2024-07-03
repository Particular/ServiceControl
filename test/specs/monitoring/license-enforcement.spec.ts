import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { expiredLicenseMessageWithValue } from "./questions/expiredLicenseMessageWithValue";
import { viewYourLicenseButton } from "./questions/viewYourLicenseButton";
import { extendYourLicenseButton } from "./questions/extendYourLicenseButton";
import { getAlertNotifications } from "./questions/alertNotifications";

describe("FEATURE: EXPIRING license detection", () => {
  describe("RULE: The user should be alerted while using the {monitoring endpoint list} functionality about an EXPIRING license", () => {
    test("EXAMPLE: Expiring trial", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(precondition.LicenseType.Trial));

      await driver.goTo("monitoring");

      const notification = (await getAlertNotifications()).find(async (n) => {
        n.textMatches(/your non\-production development license will expire soon\. to continue using the particular service platform you'll need to extend your license\./i);
      });

      expect(notification).not.toBeUndefined();
      expect(notification?.hasLink({ caption: "Extend your license", address: "http://particular.net/extend-your-trial?p=servicepulse" })).toBeTruthy();
      expect(notification?.hasLink({ caption: "View license details", address: "#/configuration" })).toBeTruthy();
    });

    [
      { description: "Expiring upgrade protection", linceseType: precondition.LicenseType.UpgradeProtection },
      { description: "Expiring platform subscription", linceseType: precondition.LicenseType.Subscription },
    ].forEach(({ description, linceseType }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(linceseType));

        await driver.goTo("monitoring");

        const notification = (await getAlertNotifications()).find(async (n) => {
          n.textMatches(/once upgrade protection expires, you'll no longer have access to support or new product versions/i);
        });

        expect(notification).not.toBeUndefined();
        expect(notification?.hasLink({ caption: "View license details", address: "#/configuration" })).toBeTruthy();
      });
    });
  });
});

describe("FEATURE: EXPIRED license detection", () => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;

  //As of the moment of writing this test, license check is performed during the first load of the application only. No continuous check is performed.
  describe("RULE: Access to the {monitoring endpoint list} functionality should be blocked when a expired license is detected and a notification should be displayed", () => {
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
      const notification = (await getAlertNotifications()).find(async (n) => {
        n.textMatches(/your license has expired\. please contact particular software support at:/i);
      });

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
      const notification = (await getAlertNotifications()).find(async (n) => {
        n.textMatches(/your license has expired\. please contact particular software support at:/i);
      });

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
      const notification = (await getAlertNotifications()).find(async (n) => {
        n.textMatches(/your license has expired\. please contact particular software support at:/i);
      });

      expect(notification).not.toBeUndefined();
      expect(notification?.hasLink({ caption: "http://particular.net/support", address: "http://particular.net/support" })).toBeTruthy();
    });
  });
});
