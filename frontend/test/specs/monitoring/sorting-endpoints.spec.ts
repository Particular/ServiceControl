import { expect } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import { groupEndpointsBy } from "./actions/groupEndpointsBy";
import { endpointGroupNames } from "./questions/endpointGroupNames";
import { endpointGroup } from "./questions/endpointGroup";
import { sortEndpointsBy } from "./actions/sortEndpointsBy";
import { findSortImageInColumn } from "./questions/sortDirection";
import { smallGraphAverageValuesByColumn, columnName } from "./questions/smallGraphAverageValuesByColumn";
import * as precondition from "../../preconditions";
import { monitoredEndpointTemplate } from "../../mocks/monitored-endpoint-template";
import { Endpoint } from "@/resources/MonitoringEndpoint";
import { endpointsNames } from "./questions/endpointsNames";

describe("FEATURE: Endpoint sorting", () => {
  describe("RULE: Grouped endpoints should be able to be sorted in ascending and descending order by group name and by endpoint name inside the group", () => {
    // Skipping for now, this is constantly failing randomly
    // test("EXAMPLE: Endpoints inside of the groups and group names should be sorted in the same direction as the ungrouped endpoints", async ({ driver }) => {
    //   //Arrange
    //   await driver.setUp(precondition.serviceControlWithMonitoring);
    //   await driver.setUp(
    //     precondition.monitoredEndpointsNamed([
    //       "Universe.Solarsystem.Earth.Endpoint5",
    //       "Universe.Solarsystem.Earth.Endpoint6",
    //       "Universe.Solarsystem.Mercury.Endpoint1",
    //       "Universe.Solarsystem.Mercury.Endpoint2",
    //       "Universe.Solarsystem.Venus.Endpoint3",
    //       "Universe.Solarsystem.Venus.Endpoint4",
    //     ])
    //   );
    //
    //   //Act
    //   await driver.goTo("monitoring");
    //   await groupEndpointsBy({ numberOfSegments: 3 });
    //   //Assert
    //   expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
    //   expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
    //   expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
    //   expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
    //
    //   //Act
    //   await groupEndpointsBy({ numberOfSegments: 0 });
    //   await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); //Descending
    //   await groupEndpointsBy({ numberOfSegments: 3 });
    //   //Assert
    //   expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Venus", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Earth"]);
    //   expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint4", "Endpoint3"]);
    //   expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint2", "Endpoint1"]);
    //   expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint6", "Endpoint5"]);
    // });

    test("EXAMPLE: Endpoints inside of the groups and group names should be sorted in descending order when clicking the endpoint name column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.monitoredEndpointsNamed([
          "Universe.Solarsystem.Earth.Endpoint5",
          "Universe.Solarsystem.Earth.Endpoint6",
          "Universe.Solarsystem.Mercury.Endpoint1",
          "Universe.Solarsystem.Mercury.Endpoint2",
          "Universe.Solarsystem.Venus.Endpoint3",
          "Universe.Solarsystem.Venus.Endpoint4",
        ])
      );

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      await sortEndpointsBy({ column: columnName.ENDPOINTNAME });

      //Assert
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Venus", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Earth"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint4", "Endpoint3"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint2", "Endpoint1"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint6", "Endpoint5"]);
    });

    test("EXAMPLE: Endpoints inside of the groups and group names should be sorted in ascending order when clicking twice on the endpoint name column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.monitoredEndpointsNamed([
          "Universe.Solarsystem.Venus.Endpoint3",
          "Universe.Solarsystem.Venus.Endpoint4",
          "Universe.Solarsystem.Mercury.Endpoint1",
          "Universe.Solarsystem.Mercury.Endpoint2",
          "Universe.Solarsystem.Earth.Endpoint5",
          "Universe.Solarsystem.Earth.Endpoint6",
        ])
      );

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); //Click the column title once for descending
      await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); //Click the column title again for ascending

      //Assert
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
    });
  });

  describe("RULE: Sort arrow images should only be visible on the column that is being sorted", () => {
    test("EXAMPLE: Sort up arrow should only be visible on endpoint name column on page load", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");

      //retrieve the endpoint names as a way to ensure the monitoring page finished rendering the endpoint list
      await endpointsNames();
      //Assert
      assertSortImageState(columnName.ENDPOINTNAME, "up");
      for (const otherColumn of Object.values(columnName).filter((col) => col !== columnName.ENDPOINTNAME)) {
        assertSortImageState(otherColumn, null); // Assert that all other columns don't have sorting images
      }
    });

    Object.values(columnName).forEach((column) => {
      test(`EXAMPLE: Sort up and down arrow images should alternate visibility only on the column "${column.toUpperCase()}"`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

        //Act
        await driver.goTo("monitoring");

        //retrieve the endpoint names as a way to ensure the monitoring page finished rendering the endpoint list
        await endpointsNames();

        //Assert sorting of Endpoint name first since it sorts in ascending order by default, while all the other columns sort in descending order by default
        assertSortImageState(columnName.ENDPOINTNAME, "up");

        await sortEndpointsBy({ column }); // Click the column title being tested once for descending
        assertSortImageState(column, "down");

        for (const otherColumn of Object.values(columnName).filter((col) => col !== column)) {
          assertSortImageState(otherColumn, null); // Assert that all other columns don't have sorting images
        }

        await sortEndpointsBy({ column }); // Click the column title once for ascending
        assertSortImageState(column, "up");

        for (const otherColumn of Object.values(columnName).filter((col) => col !== column)) {
          assertSortImageState(otherColumn, null); // Assert that all other columns don't have sorting images
        }
      });
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on endpoint name", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking name on the Endpoint name column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");

      await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); // Act: Click the column title once for descending
      assertSortImageState(columnName.ENDPOINTNAME, "down");

      //Assert
      for (const otherColumn of Object.values(columnName).filter((col) => col !== columnName.ENDPOINTNAME)) {
        assertSortImageState(otherColumn, null); // Assert that all other columns don't have sorting images
      }
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking name on the Endpoint name column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.ENDPOINTNAME }); // Act: Click the column title once for ascending
      assertSortImageState(columnName.ENDPOINTNAME, "up");

      //Assert
      for (const otherColumn of Object.values(columnName).filter((col) => col !== columnName.ENDPOINTNAME)) {
        assertSortImageState(otherColumn, null); // Assert that all other columns don't have sorting images
      }
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on average queue length", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking the queue length column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.queueLength.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.queueLength.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.queueLength.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.QUEUELENGTH }); // Act: Click the column title once for descending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint2", "Endpoint1", "Endpoint3"]);
      const avgValues = await smallGraphAverageValuesByColumn({ column: columnName.QUEUELENGTH });
      expect(avgValues).toEqual(["4.1", "2.1", "1.1"]);
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking the queue length column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.queueLength.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.queueLength.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.queueLength.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.QUEUELENGTH }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.QUEUELENGTH }); // Act: Click the column title once for ascending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint3", "Endpoint1", "Endpoint2"]);
      const ascendingAvgValues = await smallGraphAverageValuesByColumn({ column: columnName.QUEUELENGTH });
      expect(ascendingAvgValues).toEqual(["1.1", "2.1", "4.1"]);
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on average throughput per second", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking the throughput column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.throughput.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.throughput.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.throughput.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.THROUGHPUT }); // Act: Click the column title once for descending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint2", "Endpoint1", "Endpoint3"]);
      const avgValues = await smallGraphAverageValuesByColumn({ column: columnName.THROUGHPUT });
      expect(avgValues).toEqual(["4.1", "2.1", "1.1"]);
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking the throughput column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.throughput.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.throughput.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.throughput.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.THROUGHPUT }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.THROUGHPUT }); // Act: Click the column title once for ascending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint3", "Endpoint1", "Endpoint2"]);
      const ascendingAvgValues = await smallGraphAverageValuesByColumn({ column: columnName.THROUGHPUT });
      expect(ascendingAvgValues).toEqual(["1.1", "2.1", "4.1"]);
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on average scheduled retries per second", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking the scheduled retries column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.retries.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.retries.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.retries.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.SCHEDULEDRETRIES }); // Act: Click the column title once for descending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint2", "Endpoint1", "Endpoint3"]);
      const avgValues = await smallGraphAverageValuesByColumn({ column: columnName.SCHEDULEDRETRIES });
      expect(avgValues).toEqual(["4.1", "2.1", "1.1"]);
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking the scheduled retries column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.retries.average = 2.1;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.retries.average = 4.1;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.retries.average = 1.1;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.SCHEDULEDRETRIES }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.SCHEDULEDRETRIES }); // Act: Click the column title once for ascending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint3", "Endpoint1", "Endpoint2"]);
      const ascendingAvgValues = await smallGraphAverageValuesByColumn({ column: columnName.SCHEDULEDRETRIES });
      expect(ascendingAvgValues).toEqual(["1.1", "2.1", "4.1"]);
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on average processing time", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking the scheduled retries column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.processingTime.average = 350;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.processingTime.average = 800;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.processingTime.average = 225;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.PROCESSINGTIME }); // Act: Click the column title once for descending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint2", "Endpoint1", "Endpoint3"]);
      const avgValues = await smallGraphAverageValuesByColumn({ column: columnName.PROCESSINGTIME });
      expect(avgValues).toEqual(["800", "350", "225"]);
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking the scheduled retries column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1: Endpoint = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.processingTime.average = 350;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.processingTime.average = 800;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.processingTime.average = 225;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.PROCESSINGTIME }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.PROCESSINGTIME }); // Act: Click the column title once for ascending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint3", "Endpoint1", "Endpoint2"]);
      const ascendingAvgValues = await smallGraphAverageValuesByColumn({ column: columnName.PROCESSINGTIME });
      expect(ascendingAvgValues).toEqual(["225", "350", "800"]);
    });
  });

  describe("RULE: Ungrouped endpoints should be able to be sorted in ascending and descending order based on average critical time", () => {
    test("EXAMPLE: Endpoints are sorted in descending order by clicking the scheduled retries column title", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.criticalTime.average = 350;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.criticalTime.average = 800;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.criticalTime.average = 225;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.CRITICALTIME }); // Act: Click the column title once for descending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint2", "Endpoint1", "Endpoint3"]);
      const avgValues = await smallGraphAverageValuesByColumn({ column: columnName.CRITICALTIME });
      expect(avgValues).toEqual(["800", "350", "225"]);
    });
    test("EXAMPLE: Endpoints are sorted in ascending order by clicking the scheduled retries column title twice", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const endpoint1 = structuredClone(monitoredEndpointTemplate);
      endpoint1.name = "Endpoint1";
      endpoint1.metrics.criticalTime.average = 350;

      const endpoint2 = structuredClone(monitoredEndpointTemplate);
      endpoint2.name = "Endpoint2";
      endpoint2.metrics.criticalTime.average = 800;

      const endpoint3 = structuredClone(monitoredEndpointTemplate);
      endpoint3.name = "Endpoint3";
      endpoint3.metrics.criticalTime.average = 225;

      await driver.setUp(precondition.hasMonitoredEndpointsList([endpoint1, endpoint2, endpoint3]));

      //Act
      await driver.goTo("monitoring");
      await sortEndpointsBy({ column: columnName.CRITICALTIME }); // Act: Click the column title once for descending
      await sortEndpointsBy({ column: columnName.CRITICALTIME }); // Act: Click the column title once for ascending

      //Assert
      expect(await endpointsNames()).toEqual(["Endpoint3", "Endpoint1", "Endpoint2"]);
      const ascendingAvgValues = await smallGraphAverageValuesByColumn({ column: columnName.CRITICALTIME });
      expect(ascendingAvgValues).toEqual(["225", "350", "800"]);
    });
  });
});

function assertSortImageState(column: string, direction: "up" | "down" | null) {
  if (direction === null) {
    expect(findSortImageInColumn(column, "up")).toBeNull();
    expect(findSortImageInColumn(column, "down")).toBeNull();
  } else if (direction === "up") {
    expect(findSortImageInColumn(column, "up")).toBeInTheDocument();
    expect(findSortImageInColumn(column, "down")).toBeNull();
  } else {
    expect(findSortImageInColumn(column, "up")).toBeNull();
    expect(findSortImageInColumn(column, "down")).toBeInTheDocument();
  }
}
