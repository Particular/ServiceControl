import { expect } from "vitest";
import { it, describe } from "../../drivers/vitest/driver";
import { waitFor, screen } from "@testing-library/vue";
import { enterFilterString } from "./actions/enterFilterString";
import { endpointWithName } from "./questions/endpointWithName";
import { groupEndpointsBy } from "./actions/groupEndpointsBy";
import { endpointGroupNames } from "./questions/endpointGroupNames";
import { endpointGroup } from "./questions/endpointGroup";
import { filteredByName } from "./questions/filteredByName";

import * as precondition from "../../preconditions";

describe("FEATURE: Endpoint filtering", () => {
  describe("RULE: List of monitoring endpoints should be filterable by the name", () => {
    it("Example: Filter string matches full endpoint name", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("Universe.Solarsystem.Earth.Endpoint1");

      //Assert
      //Confirm Endpoint1 still shows in the list after filtering
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();

      //Confirm Endpoint2 and Endpoint3 no longer shows in the list after filtering
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });
    it("Example: Filter string matches a substring of only 1 endpoint name", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("Endpoint1");

      //Assert
      //Confirm Endpoint1 still shows in the list after filtering
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      //Confirm Endpoint2 and Endpoint3 no longer shows in the list after filtering
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });

    it("Example: Filter string doesn't match any endpoint name", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("WrongName");

      //Assert
      //Confirm no endpoints shows in the list after filtering
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });

    it("Example: Enter filter string that matches 1 endpoint and clearing the filter string should display all endpoints", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("Endpoint1");
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
      await enterFilterString("");

      //Assert
      //Confirm all endpoints shows in the list after clearing the filter string
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument();
    });

    it("Example: No filter string is entered and all endpoints should be displayed", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");

      //Assert
      //Confirm all endpoints shows in the list after clearing the filter string
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
    });
  });

  describe("Rule: Filtering by endpoint name should be case insensitive", () => {
    it("Example: All upper case letters are used for a filter string that matches only 1 endpoint", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("ENDPOINT1");

      //Assert
      //Confirm all endpoints shows in the list after clearing the filter string
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });

    it("Example: All lower case letters are used for a filter string that matches only 1 endpoint", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("endpoint1");

      //Assert
      //Confirm all endpoints shows in the list after clearing the filter string
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });

    it("Example: A mix of upper and lower case letters are used for a filter string that matches only 1 endpoint", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.monitoredEndpointsNamed(["Universe.Solarsystem.Earth.Endpoint1", "Universe.Solarsystem.Earth.Endpoint2", "Universe.Solarsystem.Earth.Endpoint3"]));

      //Act
      await driver.goTo("monitoring");
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeInTheDocument());
      await enterFilterString("EnDpOiNt1");

      //Assert
      //Confirm all endpoints shows in the list after clearing the filter string
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint1")).toBeInTheDocument();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint2")).toBeNull();
      expect(endpointWithName("Universe.Solarsystem.Earth.Endpoint3")).toBeNull();
    });
  });

  describe("Rule: Filtering by endpoint name should be possible when endpoints are grouped", () => {
    it("Example: Filter string matches only 1 endpoint in only 1 group", async ({ driver }) => {
      //Arrange
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

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("Endpoint1");

      //Assert
      await waitFor(() => expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Mercury"]));
      await waitFor(() => expect(endpointWithName("Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint2")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint3")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint4")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint5")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint6")).toBeNull());
    });

    it("Example: Filter string matches all endpoints in each group", async ({ driver }) => {
      //Arrange
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

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("Endpoint");

      //Assert
      await waitFor(() => expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]));
      await waitFor(() => expect(endpointWithName("Endpoint1")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint2")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint3")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint4")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint5")).toBeInTheDocument());
      await waitFor(() => expect(endpointWithName("Endpoint6")).toBeInTheDocument());
    });

    it("Example: Filter string doesn't match any endpoints in any groups", async ({ driver }) => {
      //Arrange
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

      //Act
      await driver.goTo("monitoring");
      await groupEndpointsBy({ numberOfSegments: 3 });
      expect(endpointGroupNames()).toEqual(["Universe.Solarsystem.Earth", "Universe.Solarsystem.Mercury", "Universe.Solarsystem.Venus"]);
      expect(endpointGroup("Universe.Solarsystem.Mercury").Endpoints).toEqual(["Endpoint1", "Endpoint2"]);
      expect(endpointGroup("Universe.Solarsystem.Venus").Endpoints).toEqual(["Endpoint3", "Endpoint4"]);
      expect(endpointGroup("Universe.Solarsystem.Earth").Endpoints).toEqual(["Endpoint5", "Endpoint6"]);
      await enterFilterString("WrongName");

      //Assert
      await waitFor(() => expect(endpointGroupNames()).toEqual([]));
      await waitFor(() => expect(endpointWithName("Endpoint1")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint2")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint3")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint4")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint5")).toBeNull());
      await waitFor(() => expect(endpointWithName("Endpoint6")).toBeNull());
    });
  });

  describe("Rule: Filter string can get and set the filter parameter in the permalink", () => {
    it("Example: Filter string should be updated when the permalink has the filter parameter set", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      //Act
      await driver.goTo("monitoring?filter=Endpoint1");

      //Assert
      await waitFor(() => expect(filteredByName("Endpoint1")).toBeInTheDocument());
    });

    it("Example: The permalink's filter parameter is updated when a filter string is entered", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      //Act
      await driver.goTo("monitoring");
      await enterFilterString("Endpoint1");

      //Assert
      // Wait for the current page to change since the permalink should be different
      await waitFor(() => expect(window.location.href).not.toEqual("http://localhost:3000/#/monitoring"));
      await waitFor(() => expect(window.location.href).toEqual("http://localhost:3000/#/monitoring?filter=Endpoint1"));
    });

    it("Example: The permalink's filter parameter is removed when filter string is empty", async ({ driver }) => {
      //Arrange
      await driver.setUp(precondition.serviceControlWithMonitoring);

      //Act
      await driver.goTo("monitoring?filter=Endpoint1");
      await waitFor(() => expect(filteredByName("Endpoint1")).toBeInTheDocument());
      await enterFilterString("");

      //Assert
      //Wait for the current page to change since the permalink should be different
      await waitFor(() => expect(window.location.href).not.toEqual("http://localhost:3000/#/monitoring?filter=Endpoint1"));
      await waitFor(() => expect(window.location.href).toEqual("http://localhost:3000/#/monitoring"));
    });
  });
});
