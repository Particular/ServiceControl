import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { licenseTypeDetails } from "./questions/licenseTypeDetails";
import { licenseExpiryDate } from "./questions/licenseExpiryDate";
import { licenseExpiryDaysLeft } from "./questions/licenseExpiryDaysLeft";
import { licenseExpired } from "./questions/licenseExpired";
import { waitFor } from "@testing-library/vue";
import { LicenseType } from "@/resources/LicenseInfo";
import { licenseTabList, licenseTabNames } from "./questions/licenseTabs";

describe("FEATURE: License", () => {
  describe("RULE: Platform license type should be shown", () => {
    test("EXAMPLE: The platform is running a commercial license", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasActiveLicense);
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseTypeDetails()).toBe("Commercial, Enterprise");
      });
    });
  });

  describe("RULE: License expiry date should be shown", () => {
    test("EXAMPLE: Valid license expiry date is shown", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasActiveLicense);
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDate()).toBeVisible();
      });
    });
  });

  describe("RULE: License expired", () => {
    test("EXAMPLE: An expired license", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiredLicense(LicenseType.Subscription, 5)); //license expired 6 days before
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpired()).toBe("Your license expired. Please update the license to continue using the Particular Service Platform.");
      });
    });
    test("EXAMPLE: Visiting the configuration page with an expired license", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiredLicense(LicenseType.Subscription, 5)); //license expired 6 days before
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseTabList()).toHaveLength(2);
        expect(await licenseTabNames()).toEqual(expect.arrayContaining(["License", "Usage Setup"]));
      });
    });
  });

  describe("RULE: License expiring soon must be displayed", () => {
    test("EXAMPLE: License expiring in 11 days", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.Subscription, 10));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("expiring in 11 days");
      });
    });
    test("EXAMPLE: License expiring tomorrow", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.Subscription, 0));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("expiring tomorrow");
      });
    });
    test("EXAMPLE: License expiring today", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.Subscription, -1));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("expiring today");
      });
    });
  });

  describe("RULE: Upgrade Protection license expiring soon must be displayed with a reference to how much time is left and a warning:'Once upgrade protection expires, you'll no longer have access to support or new product versions. '", () => {
    test("EXAMPLE: Upgrade Protection license expiring in 11 days", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.UpgradeProtection, 10));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("11 days left");
      });
    });
    test("EXAMPLE: Upgrade Protection license expiring tomorrow", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.UpgradeProtection, 0));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("1 day left");
      });
    });
    test("EXAMPLE: Upgrade Protection license expiring today", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasExpiringLicense(LicenseType.UpgradeProtection, -1));
      await driver.goTo("/configuration/license");
      await waitFor(async () => {
        expect(await licenseExpiryDaysLeft()).toBeVisible();
        expect((await licenseExpiryDaysLeft()).textContent).toContain("expired");
      });
    });
  });
});
