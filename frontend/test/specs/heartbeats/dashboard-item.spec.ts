import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { expect } from "vitest";
import { queryHeartbeatDashboardItem } from "./questions/queryHeartbeatDashboardItem";
import { waitFor } from "@testing-library/vue";

describe("FEATURE: Dashboard item", () => {
  describe("RULE: The count of unhealthy endpoints should be displayed", () => {
    test("EXAMPLE: No unhealthy endpoints.", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasAHealthyEndpoint());

      await driver.goTo("dashboard");

      await waitFor(async () => {
        const heartbeatDashboardItem = await queryHeartbeatDashboardItem();

        // Reverse logic here to make sure the heartbeatDashboardItem is defined but the flag is falsey.
        expect(heartbeatDashboardItem && !heartbeatDashboardItem.isCounterVisible).toBeTruthy();
      });
    });

    test("EXAMPLE: One unhealthy endpoint.", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasAnUnhealthyEndpoint());

      await driver.goTo("dashboard");

      await waitFor(async () => {
        const heartbeatDashboardItem = await queryHeartbeatDashboardItem();
        // check the endpoint data has been updated immediately
        expect(heartbeatDashboardItem && heartbeatDashboardItem.isCounterVisible).toBeTruthy();
        expect(heartbeatDashboardItem && heartbeatDashboardItem.counterValue).toBe(1);
      });
    });

    test("EXAMPLE: Three unhealthy endpoints.", async ({ driver }) => {
      const numberOfUnhealthyEndpoints = 3;

      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasUnhealthyEndpoints(numberOfUnhealthyEndpoints));

      await driver.goTo("dashboard");

      await waitFor(async () => {
        const heartbeatDashboardItem = await queryHeartbeatDashboardItem();

        expect(heartbeatDashboardItem && heartbeatDashboardItem.isCounterVisible).toBeTruthy();
        expect(heartbeatDashboardItem && heartbeatDashboardItem.counterValue).toBe(numberOfUnhealthyEndpoints);
      });
    });
  });
});
