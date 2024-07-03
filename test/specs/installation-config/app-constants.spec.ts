import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { endpointsNames } from "../monitoring/questions/endpointsNames";

describe("FEATURE: app.constants.js", () => {
  describe("RULE: The system should automatically navigate to the specified path in default_route property", () => {
    test("EXAMPLE: default route is set to /dashboard", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "/dashboard";

      //act
      await driver.goTo("/");

      expect(window.location.href).toBe("http://localhost:3000/#/dashboard");
    });

    test("EXAMPLE: default route is set to /monitoring", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1"]));
      window.defaultConfig.default_route = "/monitoring";

      //act
      await driver.goTo("/");

      //Although the navigation was not explicitly made to /monitoring, the default route should be applied and therefore:
      // - the monitoring page should be displayed
      expect(await endpointsNames()).toEqual(["Endpoint1"]);
      //- the monitoring address should be in the URL
      expect(window.location.href).toBe("http://localhost:3000/#/monitoring");
    });

    test("EXAMPLE: default route is set to a empty value", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "";

      //act
      await driver.goTo("");

      expect(window.location.href).toBe("http://localhost:3000/#/");
    });

    test("EXAMPLE: default route is set to /", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "/";

      //act
      await driver.goTo("");

      expect(window.location.href).toBe("http://localhost:3000/#/");
    });
  });
});
