import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import { screen } from "@testing-library/vue";
import * as precondition from "../../preconditions";

describe("FEATURE: Endpoint listing", () => {
  describe("RULE: It should be indicated when there is connectivity to the backend but there is no endpoints found (no data)", () => {
    test("EXAMPLE: No monitored endpoints", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasNoMonitoredEndpoints);
      //Act
      await driver.goTo("monitoring");

      expect(await screen.findByText(/the monitoring service is active but no data is being returned\./i)).toBeInTheDocument();
    });
  });
});
