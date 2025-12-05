import { vi, expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import { enterFilterString } from "./actions/enterFilterString";
import { groupEndpointsBy } from "./actions/groupEndpointsBy";
import { endpointGroupNames } from "./questions/endpointGroupNames";
import { endpointGroup } from "./questions/endpointGroup";
import { currentFilterValueToBe } from "./questions/currentFilterValueToBe";
import { endpointsNames } from "./questions/endpointsNames";
import * as precondition from "../../preconditions";

vi.mock("@vueuse/core", async (importOriginal) => {
  const originalModule = await importOriginal<typeof import("@vueuse/core")>();
  return {
    ...originalModule,
    // eslint-disable-next-line @typescript-eslint/no-unsafe-function-type
    useDebounceFn: (fn: Function) => fn,
  };
});

describe("FEATURE: Endpoint filtering", () => {
  describe("RULE: List of monitoring endpoints should be filterable by the name", () => {
    [
      {
        description: "Filter string matches full endpoint name",
        filterString: "Universe.Solarsystem.Earth.Endpoint1",
        expectedEndpoints: ["Universe.Solarsystem.Earth.Endpoint1"],
      },
      {
        description: "Filter string matches a substring of only 1 endpoint name",
        filterString: "Endpoint1",
        expectedEndpoints: ["Universe.Solarsystem.Earth.Endpoint1"],
      },
      {
        description: "Filter string doesn't match any endpoint name",
        filterString: "WrongName",
        expectedEndpoints: [],
      },
    ].forEach((scenario) => {
      test(`EXAMPLE: ${scenario.description}`, async ({ driver }) => {
        // Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

        // Act
        await driver.goTo("monitoring");
        expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]);
        await enterFilterString(scenario.filterString);

        // Assert
        expect(await endpointsNames()).toEqual(scenario.expectedEndpoints);
      });
    });

    test("EXAMPLE: Enter filter string that matches 1 endpoint and clearing the filter string should display all endpoints", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      // Act
      await driver.goTo("monitoring");
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]);

      await enterFilterString("Endpoint1");
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1"]);

      await enterFilterString("");

      // Assert
      // Confirm all endpoints show in the list after clearing the filter string
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]);
    });

    test("EXAMPLE: No filter string is entered and all endpoints should be displayed", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      // Act
      await driver.goTo("monitoring");

      // Assert
      // Confirm all endpoints show in the list after clearing the filter string
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]);
    });
  });

  describe("RULE: Filtering by endpoint name should be case insensitive", () => {
    [
      { description: "All lower case letters are used for a filter string that matches only 1 endpoint", filterString: "endpoint1" },
      { description: "All upper case letters are used for a filter string that matches only 1 endpoint", filterString: "ENDPOINT1" },
      { description: "A mix of upper and lower case letters are used for a filter string that matches only 1 endpoint", filterString: "EnDpOiNt1" },
    ].forEach((scenario) => {
      test(`EXAMPLE: ${scenario.description}`, async ({ driver }) => {
        // Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

        // Act
        await driver.goTo("monitoring");
        expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]);
        await enterFilterString(scenario.filterString);

        // Assert
        // Confirm only endpoint1 shows in the list after filtering
        expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1"]);
      });
    });
  });

  describe("RULE: Filtering by endpoint name should be possible when endpoints are grouped", () => {
    test("EXAMPLE: Filter string matches only 1 endpoint in only 1 group", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.monitoredEndpointsNamed([
          "Universe.Solarsystem.Mercury.Endpoint1",
          "Universe.Solarsystem.Mercury.Endpoint2",
          "Universe.Solarsystem.Venus.Endpoint3",
          "Universe.Solarsystem.Venus.Endpoint4",
          "Universe.Solarsystem.Earth.Endpoint5",
          "Universe.Solarsystem.Earth.Endpoint6",
        ])
      );

      // Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("Endpoint1");

      // Assert
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Mercury"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1"]);
      expect(await endpointsNames()).toEqual(["Endpoint1"]);
    });

    test("EXAMPLE: Filter string matches all endpoints in each group", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.monitoredEndpointsNamed([
          "Universe.Solarsystem.Mercury.Endpoint1",
          "Universe.Solarsystem.Mercury.Endpoint2",
          "Universe.Solarsystem.Venus.Endpoint3",
          "Universe.Solarsystem.Venus.Endpoint4",
          "Universe.Solarsystem.Earth.Endpoint5",
          "Universe.Solarsystem.Earth.Endpoint6",
        ])
      );

      // Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("Endpoint");

      // Assert
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(await endpointsNames()).toEqual(["Endpoint5", "Endpoint6", "Endpoint1", "Endpoint2", "Endpoint3", "Endpoint4"]);
    });

    test("EXAMPLE: Filter string doesn't match any endpoints in any groups", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.monitoredEndpointsNamed([
          "Universe.Solarsystem.Mercury.Endpoint1",
          "Universe.Solarsystem.Mercury.Endpoint2",
          "Universe.Solarsystem.Venus.Endpoint3",
          "Universe.Solarsystem.Venus.Endpoint4",
          "Universe.Solarsystem.Earth.Endpoint5",
          "Universe.Solarsystem.Earth.Endpoint6",
        ])
      );

      // Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("WrongName");

      // Assert
      expect(endpointGroupNames()).toEqual([]);
      expect(await endpointsNames()).toEqual([]);
    });
  });

  describe("RULE: Filter string can get and set the filter parameter in the permalink", () => {
    test("EXAMPLE: Filter string should be updated when the permalink has the filter parameter set", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      //Setup at least one endpoint to prevent the no-data screen to show, which would prevent the filter input from being displayed
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1"]));
      //Act
      await driver.goTo("monitoring?filter=Endpoint1");
      //Retrieve the endpoints names to give time for endpoints list to render and parse the filter parameter from the URL. This functions awaits until the endpoint list gets rendered.
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1"]);

      //Assert
      expect(currentFilterValueToBe("Endpoint1")).toBeTruthy();
    });

    test("EXAMPLE: The permalink's filter parameter is updated when a filter string is entered", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      //Setup at least one endpoint to prevent the no-data screen to show, which would prevent the filter input from being displayed
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1"]));

      //Act
      await driver.goTo("monitoring");
      await enterFilterString("Endpoint1");

      //Assert
      expect(window.location.href).toEqual("http://localhost:3000/#/monitoring?historyPeriod=1&filter=Endpoint1");
    });

    test("EXAMPLE: The permalink's filter parameter is removed when filter string is empty", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      //Setup at least one endpoint to prevent the no-data screen to show, which would prevent the filter input from being displayed
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1"]));

      //Act
      await driver.goTo("monitoring?filter=Endpoint1");
      //Retrieve the endpoints names to give time for endpoints list to render and parse the filter parameter from the URL. This functions awaits until the endpoint list gets rendered.
      expect(await endpointsNames()).toEqual(["Universe.Solarsystem.Earth.Endpoint1"]);

      expect(currentFilterValueToBe("Endpoint1")).toBeTruthy();
      await enterFilterString("");

      //Assert
      expect(window.location.href).toEqual("http://localhost:3000/#/monitoring");
    });
  });
});
