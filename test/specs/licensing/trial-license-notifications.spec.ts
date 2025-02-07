import { screen, waitFor } from "@testing-library/vue";
import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { getTrialBar } from "./questions/trialLicenseBar";
import { LicenseType } from "@/resources/LicenseInfo";
import flushPromises from "flush-promises";

describe("FEATURE: Trial license notifications", () => {
  describe("RULE: The user should know they are using a trial license at all times", () => {
    [{ viewname: "dashboard" }, { viewname: "configuration" }, { viewname: "monitoring" }].forEach(({ viewname }) => {
      test(`EXAMPLE: ${viewname}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(LicenseType.Trial));

        await driver.goTo(viewname);

        const trialBar = await getTrialBar();
        expect(trialBar.textMatches(/non-production use only/i)).toBeTruthy();
        expect(trialBar.hasLinkWithCaption("Trial license").address).toBe("#/configuration/license");

        await flushPromises();
      });
    });
  });
  describe("RULE: The user should not see trial license information for commercial licenses", () => {
    [{ viewname: "configuration" }, { viewname: "dashboard" }, { viewname: "monitoring" }].forEach(({ viewname }) => {
      test(`EXAMPLE: ${viewname}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasExpiringLicense(LicenseType.Subscription));

        await driver.goTo(viewname);

        //This has to use waitFor because of shared state between test runs. See issue documented issue and proposed solution https://github.com/Particular/ServicePulse/issues/1905
        await waitFor(() => expect(screen.queryByRole("status", { name: /trial license bar information/i })).toBeNull());

        await flushPromises();
      });
    });
  });
});
