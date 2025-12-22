import { expect, vi } from "vitest";
import { test, describe } from "../../drivers/vitest/driver";
import { waitFor } from "@testing-library/vue";
import * as precondition from "../../preconditions";
import { selectHistoryPeriod } from "./actions/selectHistoryPeriod";
import { endpointSparklineValues } from "./questions/endpointSparklineValues";
import { historyPeriodSelected } from "./questions/historyPeriodSelected";
import { endpointDetailsLinks } from "./questions/endpointDetailLinks";

describe("FEATURE: Endpoint history periods", () => {
  describe("RULE: History period should get and set the permalink history period query parameter", () => {
    [
      { description: "History period '1m' selected and permalink history period query parameter should be set to 1", historyPeriod: 1 },
      { description: "History period '5m' selected and permalink history period query parameter should be set to 5", historyPeriod: 5 },
      { description: "History period '10m' selected and permalink history period query parameter should be set to 10", historyPeriod: 10 },
      { description: "History period '15m' selected and permalink history period query parameter should be set to 15", historyPeriod: 15 },
      { description: "History period '30m' selected and permalink history period query parameter should be set to 30", historyPeriod: 30 },
      { description: "History period '1h' selected and permalink history period query parameter should be set to 60", historyPeriod: 60 },
    ].forEach(({ description, historyPeriod }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1"]));

        //Act
        await driver.goTo(`monitoring`);
        await selectHistoryPeriod(historyPeriod);

        //Assert
        expect(window.location.href).toEqual(`http://localhost:3000/#/monitoring?historyPeriod=${historyPeriod}`);
      });
    });
    [
      { description: "History period query parameter is set to 1 and history period '1m' should be selected", historyPeriod: 1 },
      { description: "History period query parameter is set to 5 and history period '5m' should be selected", historyPeriod: 10 },
      { description: "History period query parameter is set to 10 and history period '10m' should be selected", historyPeriod: 15 },
      { description: "History period query parameter is set to 15 and history period '15m' should be selected", historyPeriod: 30 },
      { description: "History period query parameter is set to 30 and history period '30m' should be selected", historyPeriod: 30 },
      { description: "History period query parameter is set to 60 and history period '1h' should be selected", historyPeriod: 60 },
    ].forEach(({ description, historyPeriod }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1"]));

        //Act
        await driver.goTo(`monitoring?historyPeriod=${historyPeriod}`);

        //Assert
        expect(await historyPeriodSelected(historyPeriod)).toEqual("true");
      });
    });
    test("EXAMPLE: No history query parameter set and History period '1m' should be selected", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1"]));

      //Act
      await driver.goTo("monitoring");

      //Assert
      expect(await historyPeriodSelected(1)).toEqual("true");
    });
    [
      { description: "Selecting history period '1m' should update the endpoint name link with the history period selected for details", historyPeriod: 1 },
      { description: "Selecting history period '10m' should update the endpoint name link with the history period selected for details", historyPeriod: 15 },
      { description: "Selecting history period '15m' should update the endpoint name link with the history period selected for details", historyPeriod: 10 },
      { description: "Selecting history period '30m' should update the endpoint name link with the history period selected for details", historyPeriod: 30 },
      { description: "Selecting history period '1h' should update the endpoint name link with the history period selected for details", historyPeriod: 60 },
    ].forEach(({ description, historyPeriod }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1", "Endpoint2"]));

        //Act
        await driver.goTo(`monitoring`);
        await selectHistoryPeriod(historyPeriod);

        //Assert
        expect(await endpointDetailsLinks()).toEqual([`#/monitoring/endpoint/Endpoint1?historyPeriod=${historyPeriod}`, `#/monitoring/endpoint/Endpoint2?historyPeriod=${historyPeriod}`]);
      });
    });
  });
  describe("RULE: Endpoint history period data should be displayed immediately after the history period is updated", () => {
    test(`EXAMPLE: As history periods are selected the endpoint sparkline data should update immediately`, async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasEndpointWithMetricsPoints(14, 9.28, 13.8, 76, 217));

      //Act & Assert
      await driver.goTo(`monitoring`);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["14", "9.28", "13.8", "76", "217"]);

      await driver.setUp(precondition.hasEndpointWithMetricsPoints(2.96, 2.26, 2.1, 36, 147));
      await selectHistoryPeriod(5);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["2.96", "2.26", "2.1", "36", "147"]);

      await driver.setUp(precondition.hasEndpointWithMetricsPoints(10, 6.98, 9.97, 63, 194));
      await selectHistoryPeriod(10);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["10", "6.98", "9.97", "63", "194"]);

      await driver.setUp(precondition.hasEndpointWithMetricsPoints(3.65, 2.7, 2.84, 39, 152));
      await selectHistoryPeriod(15);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["3.65", "2.7", "2.84", "39", "152"]);

      await driver.setUp(precondition.hasEndpointWithMetricsPoints(12, 7.87, 11.45, 68, 203));
      await selectHistoryPeriod(30);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["12", "7.87", "11.45", "68", "203"]);

      await driver.setUp(precondition.hasEndpointWithMetricsPoints(13, 8.37, 11.61, 72, 206));
      await selectHistoryPeriod(60);
      expect(await endpointSparklineValues("Endpoint1")).toEqual(["13", "8.37", "11.61", "72", "206"]);
    });
  });
  describe("RULE: Endpoint history period data should be displayed at the interval selected by the history period", () => {
    [
      { description: "As history period is changed to 5 minutes the endpoint sparkline data should be updated at the correct interval", historyPeriod: 5 },
      { description: "As history period is changed to 10 minutes the endpoint sparkline data should be updated at the correct interval", historyPeriod: 10 },
      { description: "As history period is changed to 15 minutes the endpoint sparkline data should be updated at the correct interval", historyPeriod: 15 },
      { description: "As history period is changed to 30 minutes the endpoint sparkline data should be updated at the correct interval", historyPeriod: 30 },
      { description: "As history period is changed to 60 minutes the endpoint sparkline data should be updated at the correct interval", historyPeriod: 60 },
    ].forEach(({ description, historyPeriod }) => {
      test(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        vi.useFakeTimers(); // Needs to be called before the first call to setInterval
        await driver.setUp(precondition.serviceControlWithMonitoring);

        //Act & Assert
        await driver.goTo(`monitoring`);

        // Update the mocked data to what the backed should respond with when the fetching happens
        await driver.setUp(precondition.hasEndpointWithMetricsPoints(12, 9.56, 13.24, 81, 215));
        // simulate clicking on the history period buttons
        await selectHistoryPeriod(historyPeriod, true);

        // Wait for component to update from selected history period
        await waitFor(async () => {
          // check the endpoint data has been updated immediately
          expect(await endpointSparklineValues("Endpoint1")).toEqual(["12", "9.56", "13.24", "81", "215"]);
        });

        // Update the mocked data to what the backend should respond with when the fetching happens
        await driver.setUp(precondition.hasEndpointWithMetricsPoints(12, 9.56, 13.24, 81, 220));

        // Simulate the time passing for half the selected history period
        await vi.advanceTimersByTimeAsync((historyPeriod * 1000) / 2);
        expect(await endpointSparklineValues("Endpoint1")).toEqual(["12", "9.56", "13.24", "81", "215"]);

        // Simulate the time passing for all except 1 millisecond of the selected history period
        await vi.advanceTimersByTimeAsync((historyPeriod * 1000) / 2 - 1);
        expect(await endpointSparklineValues("Endpoint1")).toEqual(["12", "9.56", "13.24", "81", "215"]);

        // Simulate the time passing for the last millisecond to make the selected history period time now be elapsed
        await vi.advanceTimersByTimeAsync(1);

        expect(await endpointSparklineValues("Endpoint1")).toEqual(["12", "9.56", "13.24", "81", "220"]);

        vi.useRealTimers();
      });
    });
  });
});
