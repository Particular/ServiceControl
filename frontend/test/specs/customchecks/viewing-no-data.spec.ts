import { test, describe } from "../../drivers/vitest/driver";
import { expect } from "vitest";
import * as precondition from "../../preconditions";
import { customChecksMessage } from "./questions/failedCustomChecks";
import { waitFor } from "@testing-library/vue";

describe("FEATURE: No data", () => {
  describe("RULE: When there is no data to show, a message should be displayed ", () => {
    test("EXAMPLE: There are no failed or passing custom checks", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasCustomChecksEmpty);

      await driver.goTo("/custom-checks");

      await waitFor(() => {
        expect(customChecksMessage()).toBe("No failed custom checks");
      });
    });
    test("EXAMPLE: There are custom checks but none of them are failing", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const failingCustomCheckCount = 0;
      const passingCustomCheckCount = 5;
      await driver.setUp(precondition.hasCustomChecks(failingCustomCheckCount, passingCustomCheckCount));

      await driver.goTo("/custom-checks");

      await waitFor(() => {
        expect(customChecksMessage()).toBe("No failed custom checks");
      });
    });
  });
});
