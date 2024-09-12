import { screen, within } from "@testing-library/vue";
import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { waitFor } from "@testing-library/dom";
import { fail } from "assert";

describe("FEATURE: Trial license notifications", () => {
  describe("RULE: The user should know it is using a trial license at all times", () => {
    [
        { viewname: "dashboard"},
        { viewname: "configuration"},
        { viewname: "monitoring"},
    ].forEach(({ viewname }) => {
      test(`EXAMPLE: ${viewname}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(precondition.LicenseType.Trial));

        await driver.goTo(viewname);

        waitFor(()=>expect(screen.queryByRole("status", { name: /trial license bar information/i})).toBeInTheDocument())

        const licenseBarVisible =  await screen.findByRole("status", { name: /trial license bar information/i });

        expect(licenseBarVisible).not.toBeNull();
      });
    });
  });
  describe("RULE: The user should not see trial license information for commercial licenses", () => {
    [
        { viewname: "configuration"},
        { viewname: "dashboard"},
        { viewname: "monitoring"},
    ].forEach(({ viewname }) => {
      test(`EXAMPLE: ${viewname}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(precondition.LicenseType.Subscription));

        await driver.goTo(viewname);

        try {
            await screen.findByRole("status", { name: /trial license bar information/i}) 
        } catch (error) {
            return;
        }

        fail("Trial license information must not be visible");
      });
    });
  });

});
