import { expect } from "vitest";
import { screen } from "@testing-library/dom";
import { it, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { selectHistoryPeriod } from "./actions/selectHistoryPeriod";
import { historyPeriodSelected } from "./questions/historyPeriodSelected";
import { endpointDetailsLinks } from "./questions/endpointDetailsLinks";

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
      it(`EXAMPLE: ${description}`, async ({ driver }) => {
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
      it(`EXAMPLE: ${description}`, async ({ driver }) => {
        //Arrange
        await driver.setUp(precondition.serviceControlWithMonitoring);
        await driver.setUp(precondition.monitoredEndpointsNamed(["Endpoint1"]));

        //Act
        await driver.goTo(`monitoring?historyPeriod=${historyPeriod}`);

        //Assert
        expect(await historyPeriodSelected(historyPeriod)).toEqual("true");
      });
    });
    it("EXAMPLE: No history query parameter set and History period '1m' should be selected", async ({ driver }) => {
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
      it(`EXAMPLE: ${description}`, async ({ driver }) => {
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
});
