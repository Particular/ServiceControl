import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { numberOfGroupingSegments } from "./questions/numberOfGroupingSegments";
import { groupingOptionWithName } from "./questions/groupingOptionWithName";
import { openGroupingOptions } from "./actions/openGroupingOptions";
import { groupingOptions } from "./questions/groupingOptions";
import { groupEndpointsBy } from "./actions/groupEndpointsBy";
import { endpointGroupNames } from "./questions/endpointGroupNames";
import { endpointGroup } from "./questions/endpointGroup";

describe("FEATURE: Grouping endpoints", () => {
  describe("RULE: The number of grouping segments is determined by the number of periods in the endpoint name", () => {
    test("EXAMPLE: All endpoints have three periods in their name", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await openGroupingOptions();

      //Assert
      expect(await groupingOptionWithName(/max\. 1 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 2 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 3 segments/i)).toBeInTheDocument();
      expect(await numberOfGroupingSegments()).toBe(3);
    });

    test("EXAMPLE: One endpoint has three periods in its name and the other endpoints have two periods in their name", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Endpoint2", "Universe.Solarsystem.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await openGroupingOptions();

      //Assert
      expect(await groupingOptionWithName(/max\. 1 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 2 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 3 segments/i)).toBeInTheDocument();
      expect(await numberOfGroupingSegments()).toBe(3);
    });

    test("EXAMPLE: All endpoints have a different number of periods in their respective names", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Endpoint1", "Universe.Solarsystem.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await openGroupingOptions();

      //Assert
      expect(await groupingOptionWithName(/max\. 1 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 2 segments/i)).toBeInTheDocument();
      expect(await groupingOptionWithName(/max\. 3 segments/i)).toBeInTheDocument();
      expect(await numberOfGroupingSegments()).toBe(3);
    });

    test("EXAMPLE: One endpoint does not have a period in its name and the other endpoints have one period in their respective names", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1", "Universe.Endpoint2", "Universe.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await openGroupingOptions();

      //Assert
      expect(await groupingOptionWithName(/max\. 1 segments/i)).toBeInTheDocument();
      expect(await numberOfGroupingSegments()).toBe(1);
    });

    test("EXAMPLE: No endpoints have a period in their respective names", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1", "Endpoint2", "Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await openGroupingOptions();

      //Assert
      expect(await groupingOptionWithName(/no grouping/i)).toBeInTheDocument();
      expect(groupingOptions().length).toBe(1);
    });
  });

  describe("RULE: Allow the user to group endpoints by the number of segments in the endpoint name", () => {
    test("EXAMPLE: Grouping by ONE segment", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Alphacentauri.Proximacentauri.Endpoint1", "Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2"]));

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 1 });

      //Assert
      expect(endpointGroupNames()).toEqual(["Universe"]);
      expect(endpointGroup("Universe").Endpoints).toEqual(["Alphacentauri.Proximacentauri.Endpoint1", "Solarsystem.Earth.Endpoint1", "Solarsystem.Earth.Endpoint2"]);
    });

    test("EXAMPLE: Grouping by ONE segment when a single endpoint does not have a segment to be grouped by", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 1 });

      //Assert
      expect(endpointGroupNames()).toEqual(["Ungrouped", "Universe"]);
      expect(endpointGroup("Ungrouped").Endpoints).toEqual(["Endpoint1"]);
      expect(endpointGroup("Universe").Endpoints).toEqual(["Solarsystem.Earth.Endpoint2", "Solarsystem.Earth.Endpoint3"]);
    });

    test("EXAMPLE: Grouping by TWO segments", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Alphacentauri.Proximacentauri.Endpoint1", "Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2"]));

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 2 });

      //Assert
      expect(endpointGroupNames()).toEqual(["Universe.Alphacentauri", "Universe.Solarsystem"]);
      expect(endpointGroup("Universe.Alphacentauri").Endpoints).toEqual(["Proximacentauri.Endpoint1"]);
      expect(endpointGroup("Universe.Solarsystem").Endpoints).toEqual(["Earth.Endpoint1", "Earth.Endpoint2"]);
    });

    test("EXAMPLE: Grouping by TWO segments when a single endpoint only has ONE segment to be grouped by", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 2 });

      //Assert
      expect(endpointGroupNames()).toEqual(["Universe", "Universe.Solarsystem"]);
      expect(endpointGroup("Universe").Endpoints).toEqual(["Endpoint1"]);
      expect(endpointGroup("Universe.Solarsystem").Endpoints).toEqual(["Earth.Endpoint2", "Earth.Endpoint3"]);
    });
  });

  describe("RULE:  Allow the user to ungroup endpoints", () => {
    test("EXAMPLE: Select grouping by ONE segment and then select `no grouping` when all endpoints have one or more segments", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 1 });
      await groupEndpointsBy({ numberOfSegments: 0 });

      //Assert
      expect(endpointGroupNames()).toEqual([]);
    });
  });
});
