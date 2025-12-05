import { expect, vi } from "vitest";
import { waitFor } from "@testing-library/dom";
import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { endpointsDetailsTitle } from "./questions/endpointDetailsTitle";
import { endpointMessageNames, endpointMessageTypesCount } from "./questions/endpointDetailsMessageTypes";
import { endpointInstanceNames, endpointInstancesCount } from "./questions/endpointDetailsInstances";
import { endpointDetailsGraphsCurrentValues } from "./questions/endpointDetailGraphsCurrentValues";
import { endpointDetailsGraphsAverageValues } from "./questions/endpointDetailGraphsAverageValues";
import { monitoredEndpointDetails } from "../../mocks/monitored-endpoint-template";
import * as warningQuestion from "./questions/endpointWarnings";
import { selectHistoryPeriod } from "./actions/selectHistoryPeriod";
import { paginationVisible } from "./questions/paginationVisible";

describe("FEATURE: Endpoint details", () => {
  describe("RULE: Endpoint details include the endpoint name", () => {
    test("EXAMPLE: Clicking an endpoint name from the endpoint monitoring list", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpointDetails = structuredClone(monitoredEndpointDetails);
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      await driver.goTo("/monitoring/endpoint/Endpoint1");
      expect(await endpointsDetailsTitle()).toBe("Endpoint1");
    });
  });
  describe("RULE: Endpoint detail metric data should be updated immediately after changing the history period", () => {
    test(`EXAMPLE: As history periods are selected the graph data values should update immediately`, async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      const endpointDetails = structuredClone(monitoredEndpointDetails);
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      //Act
      await driver.goTo(`/monitoring/endpoint/Endpoint1`);

      // Assert
      // Wait for the default values of the page to be updated after the page is loaded
      await waitFor(async () => expect(await endpointDetailsGraphsCurrentValues()).toEqual(["2", "0", "0", "0", "0"]));
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "1.97", "0", "74", "239"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(2, 2, 8, 9.56, 13.24, 10, 81, 78, 215, 220));
      await selectHistoryPeriod(5);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["2", "8", "13.24", "81", "215"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "9.56", "10", "78", "220"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(5, 3.1, 12, 7.4, 2.2, 1, 124, 105.7, 201, 198));
      await selectHistoryPeriod(10);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["5", "12", "2.2", "124", "201"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["3.1", "7.4", "1", "105", "198"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(8, 6.5, 15, 12.6, 3.1, 2.4, 278, 255.3, 403, 387.8));
      await selectHistoryPeriod(15);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["8", "15", "3.1", "278", "403"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["6.5", "12.6", "2.4", "255", "387"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 777.7, 888.8, 999.9, 800.8));
      await selectHistoryPeriod(30);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["1.1", "3.3", "5.5", "777", "999"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["2.2", "4.4", "6.6", "888", "800"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(9.999, 8.888, 7.777, 6.666, 5.555, 4.444, 333.333, 222.222, 111.111, 100.123));
      await selectHistoryPeriod(60);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["10", "7.78", "5.55", "333", "111"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["8.89", "6.67", "4.44", "222", "100"]);

      await driver.setUp(precondition.hasEndpointWithMetricValues(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));
      await selectHistoryPeriod(1);

      expect(await endpointDetailsGraphsCurrentValues()).toEqual(["1", "3", "5", "7", "9"]);
      expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "4", "6", "8", "10"]);
    });
  });
  describe("RULE:  Endpoint detail metric data should be updated at the interval selected by the history period", () => {
    [
      { description: "As history period is changed to 5 minutes the endpoint metrics data should be updated at the correct interval", historyPeriod: 5 },
      { description: "As history period is changed to 10 minutes the endpoint metrics data should be updated at the correct interval", historyPeriod: 10 },
      { description: "As history period is changed to 15 minutes the endpoint metrics data should be updated at the correct interval", historyPeriod: 15 },
      { description: "As history period is changed to 30 minutes the endpoint metrics data should be updated at the correct interval", historyPeriod: 30 },
      { description: "As history period is changed to 60 minutes the endpoint metrics data should be updated at the correct interval", historyPeriod: 60 },
    ].forEach(({ description, historyPeriod }) => {
      //
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        // Arrange
        vi.useFakeTimers(); // Needs to be called before the first call to setInterval
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.hasEndpointWithMetricValues(2, 2, 8, 9.56, 13.24, 10, 81, 78, 215, 220));

        // Act
        await driver.goTo("/monitoring/endpoint/Endpoint1");
        await selectHistoryPeriod(historyPeriod, true);

        // Assert
        // Wait for component to update from selected history period immediately
        await waitFor(async () => expect(await endpointDetailsGraphsCurrentValues()).toEqual(["2", "8", "13.24", "81", "215"]));
        expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "9.56", "10", "78", "220"]);

        // Update the mocked data to what the backend should respond with when the fetching happens
        await driver.setUp(precondition.hasEndpointWithMetricValues(5, 3.1, 12, 7.4, 2.2, 1, 124, 105.7, 201, 198));

        // Simulate the time passing for half the selected history period
        await vi.advanceTimersByTimeAsync((historyPeriod * 1000) / 2);
        expect(await endpointDetailsGraphsCurrentValues()).toEqual(["2", "8", "13.24", "81", "215"]);
        expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "9.56", "10", "78", "220"]);

        // Simulate the time passing for all except 1 millisecond of the selected history period
        await vi.advanceTimersByTimeAsync((historyPeriod * 1000) / 2 - 1);
        expect(await endpointDetailsGraphsCurrentValues()).toEqual(["2", "8", "13.24", "81", "215"]);
        expect(await endpointDetailsGraphsAverageValues()).toEqual(["2", "9.56", "10", "78", "220"]);

        // Simulate the time passing for the last millisecond to make the selected history period time now be elapsed
        await vi.advanceTimersByTimeAsync(1);
        expect(await endpointDetailsGraphsCurrentValues()).toEqual(["5", "12", "2.2", "124", "201"]);
        expect(await endpointDetailsGraphsAverageValues()).toEqual(["3.1", "7.4", "1", "105", "198"]);

        vi.useRealTimers();
      });
    });
  });
  describe("RULE: An indication should be be displayed for the status of an endpoint", () => {
    test("EXAMPLE: An endpoint has a negative critical time", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const endpointDetails = structuredClone(monitoredEndpointDetails);
      endpointDetails.instances[0].metrics.criticalTime.points.push(-1000);
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1");

      // Assert
      await waitFor(async () => expect(await warningQuestion.negativeCriticalTimeWarning()).toBeTruthy());
    });
    test("EXAMPLE: An endpoint is stale", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const endpointDetails = structuredClone(monitoredEndpointDetails);
      endpointDetails.instances[0].isStale = true;
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1");

      // Assert
      expect(await warningQuestion.endpointStaleWarning()).toBeTruthy();
    });
    test("EXAMPLE: An endpoint is disconnected from ServiceControl monitoring", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const endpointDetails = structuredClone(monitoredEndpointDetails);
      endpointDetails.isScMonitoringDisconnected = true;
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1");

      // Assert
      expect(await warningQuestion.endpointDisconnectedWarning()).toBeTruthy();
    });

    test("EXAMPLE: An endpoint has a failed message", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      const endpointDetails = structuredClone(monitoredEndpointDetails);
      endpointDetails.errorCount = 5;
      await driver.setUp(precondition.hasMonitoredEndpointDetails(endpointDetails));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1");

      // Assert
      expect(await warningQuestion.endpointErrorCountWarning()).toBeTruthy();
      expect(await warningQuestion.endpointErrorCount()).toBe("5");
    });
  });
  describe("RULE: Endpoint details should show all message types for the endpoint", () => {
    test("EXAMPLE: The endpoint sends messages of type 'Message1,' 'Message2,' and 'Message3'", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointMessageTypesNamed(["Message1", "Message2", "Message3"]));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1");

      // Assert
      expect(await endpointMessageNames()).toEqual(["Message1", "Message2", "Message3"]);
    });
    test("EXAMPLE: Endpoint details should show correct counts for message types", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointMessageTypesNamed(["Message1", "Message2", "Message3"]));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1");

      //retrieve the endpoint message names as a way to wait for the page to finish loading
      await endpointMessageNames();
      // Assert
      expect(await endpointMessageTypesCount()).toEqual("3");
    });
  });
  describe("RULE: Endpoint details should show all instances of the endpoint", () => {
    test("EXAMPLE: The endpoint has 1 instance running", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointInstancesNamed(["Endpoint1"]));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1&tab=instancesBreakdown");

      // Assert
      expect(await endpointInstanceNames()).toEqual(["Endpoint1"]);
      expect(await endpointInstancesCount()).toEqual("1");
    });
    test("EXAMPLE: The endpoint has 3 instances running", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointInstancesNamed(["Endpoint1", "Endpoint2", "Endpoint3"]));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1?historyPeriod=1&tab=instancesBreakdown");

      // Assert
      expect(await endpointInstanceNames()).toEqual(["Endpoint1", "Endpoint2", "Endpoint3"]);
      expect(await endpointInstancesCount()).toEqual("3");
    });
  });
  describe("RULE: Pagination should be displayed when 11 or more message types are present", () => {
    test("EXAMPLE: 10 message types are present", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointMessageTypesNamed(new Array(10).fill("Message").map((name, index) => `${name}${index}`)));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1");

      // Assert
      await waitFor(() => expect(paginationVisible()).not.toBeTruthy());
    });
    test("EXAMPLE: 11 message types are present", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointMessageTypesNamed(new Array(11).fill("Message").map((name, index) => `${name}${index}`)));

      // Act
      await driver.goTo("monitoring/endpoint/Endpoint1");

      // Assert
      await waitFor(() => expect(paginationVisible()).not.toBeTruthy());
    });
    test("EXAMPLE: 12 message types are present", async ({ driver }) => {
      // Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointMessageTypesNamed(new Array(12).fill("Message").map((name, index) => `${name}${index}`)));

      // Act
      await driver.goTo("/monitoring/endpoint/Endpoint1");

      // Assert
      await waitFor(() => expect(paginationVisible()).toBeTruthy());
    });
  });
});
