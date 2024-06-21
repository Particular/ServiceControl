import { expect } from "vitest";
import { it, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { endpointsNames } from "../monitoring/questions/endpointsNames";

describe("FEATURE: app.constants.js", () => {
  describe("RULE: The system should automatically navigate to the specified path in default_route property", () => {
    it("EXAMPLE: default route is set to /dashboard", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "/dashboard";

      //act
      await driver.goTo("/");

      expect(window.location.href).toBe("http://localhost:3000/#/dashboard");
    });

    it("EXAMPLE: default route is set to /monitoring", async ({ driver }) => {
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

    it("EXAMPLE: default route is set to a empty value", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "";

      //act
      await driver.goTo("");

      expect(window.location.href).toBe("http://localhost:3000/#/");
    });

    it("EXAMPLE: default route is set to /", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      window.defaultConfig.default_route = "/";

      //act
      await driver.goTo("");

      expect(window.location.href).toBe("http://localhost:3000/#/");
    });
  });
});
