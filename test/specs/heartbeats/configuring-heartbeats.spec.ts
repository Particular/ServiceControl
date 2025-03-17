import { test, describe } from "../../drivers/vitest/driver";
import * as precondition from "../../preconditions";
import { expect } from "vitest";
import { getNothingToConfigureStatus } from "./questions/getNothingToConfigureStatus";
import { navigateToHeartbeatsConfiguration, navigateToUnHealthyHeartbeats } from "./actions/navigateToHeartbeatsTabs";
import { getEndpointsForConfiguration } from "./questions/getEndpointsForConfiguration";
import { getEndpointInstance } from "./questions/getEndpointInstance";
import { toggleHeartbeatMonitoring } from "./actions/toggleHeartbeatMonitoring";
import { getAllHeartbeatEndpointRecords, getHeartbeatEndpointRecord } from "./questions/getHeartbeatEndpointRecord";
import flushPromises from "flush-promises";
import { healthyEndpointTemplate } from "../../mocks/heartbeat-endpoint-template";
import { setHeartbeatFilter } from "./actions/setHeartbeatFilter";
import { getHeartbeatFilterValue } from "./questions/getHeartbeatFilterValue";

describe("FEATURE: Heartbeats configuration", () => {
  describe("RULE: A list of all endpoints with the heartbeats plug-in installed should be displayed", () => {
    test("EXAMPLE: With no endpoints, the text 'Nothing to configure' should be displayed", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.goTo("heartbeats/unhealthy");

      await navigateToHeartbeatsConfiguration();

      const nothingToConfigureElement = await getNothingToConfigureStatus();

      expect(nothingToConfigureElement).toBeTruthy();
      expect(nothingToConfigureElement.textContent).toBe("Nothing to configure");
    });

    test("EXAMPLE: 3 endpoints should be displayed in the list", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasHealthyEndpoints(3));
      await driver.goTo("heartbeats/unhealthy");

      await navigateToHeartbeatsConfiguration();

      const endpointRows = await getEndpointsForConfiguration();

      expect(endpointRows.size).toBe(3);
    });
  });

  describe("RULE: Toggling on/off heartbeat monitoring for endpoints should be possible", () => {
    test("EXAMPLE: Heartbeat monitoring toggle should be off by default", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.HasHealthyAndUnHealthyEndpoints(1, 1));
      await driver.goTo("heartbeats/instances/TestEndpoint_2");

      const endpointInstance = await getEndpointInstance("TestEndpoint_2");

      expect(endpointInstance.muted).toBe(false);
      await flushPromises();
    });

    /* SCENARIO
      Given a monitored endpoint instance
      When the monitoring toggle is clicked
      Then the endpoint instance is no longer monitored
      And should appear in the unhealthy endpoints list
      And should indicate the number of untracked instances.
    */
    test("EXAMPLE: Clicking the monitoring toggle for an active, healthy endpoint deactivates heartbeat monitoring for that instance", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.HasHealthyAndUnHealthyEndpoints(1, 1));
      await driver.goTo("heartbeats/instances/TestEndpoint_1");

      await toggleHeartbeatMonitoring("TestEndpoint_1");

      await driver.goTo("heartbeats/unhealthy");
      // Force all the initial pending remote calls on the page to resolve.
      await flushPromises();

      const unhealthyEndpoint = await getHeartbeatEndpointRecord("TestEndpoint_1");

      expect(unhealthyEndpoint).toBeTruthy();
      expect(unhealthyEndpoint?.instancesMuted).toBe(1);
      await flushPromises();
    });

    /* SCENARIO
      Given an unmonitored endpoint instance
      And the instance is sending heartbeats
      When the monitoring toggle is clicked
      Then the endpoint instance is monitored
      And should appear in the Healthy Endpoints list
    */
    test("EXAMPLE: Clicking the monitoring toggle for a unmonitored, healthy endpoint will activate heartbeat monitoring of the endpoint", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasAHealthyButUnMonitoredEndpoint());
      await driver.goTo("heartbeats/instances/Healthy_UnmonitoredEndpoint");

      await toggleHeartbeatMonitoring("Healthy_UnmonitoredEndpoint");

      await driver.goTo("heartbeats/healthy");
      // Force all the initial pending remote calls on the page to resolve.
      await flushPromises();

      const healthyEndpoint = await getHeartbeatEndpointRecord("Healthy_UnmonitoredEndpoint");

      expect(healthyEndpoint).toBeTruthy();
      expect(healthyEndpoint?.instancesMuted).toBe(0);
      await flushPromises();
    });

    /* SCENARIO
      Given an unmonitored endpoint instance
      And the instance is not sending heartbeats
      When the monitoring toggle is clicked
      Then the endpoint instance is monitored
      And should appear in the Unhealthy Endpoints list
    */
    test("EXAMPLE: Clicking the monitoring toggle for an unmonitored, unhealthy endpoint will activate heartbeat monitoring of the endpoint", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(precondition.hasAnUnhealthyUnMonitoredEndpoint());
      await driver.goTo("heartbeats/instances/Unhealthy_UnmonitoredEndpoint");

      await toggleHeartbeatMonitoring("Unhealthy_UnmonitoredEndpoint");

      await driver.goTo("heartbeats/unhealthy");
      // Force all the initial pending remote calls on the page to resolve.
      await flushPromises();

      const healthyEndpoint = await getHeartbeatEndpointRecord("Unhealthy_UnmonitoredEndpoint");

      expect(healthyEndpoint).toBeTruthy();
      expect(healthyEndpoint?.instancesMuted).toBe(0);
      await flushPromises();
    });
  });

  describe("RULE: Sorting by of the name of an endpoint should be possible in all displays", () => {
    test.todo("EXAMPLE: List of endpoints should be sorted by name in ascending order");

    /* SCENARIO
      Given 3 endpoint instance
        Name |
        Foo1
        Foo2
        Foo3
      When the sort by is set to Name
      Then the instances should be listed in order
    */

    /* NOTES
      Name (asc/desc)
      Latest heartbeat (asc/dec)
    */

    test.todo("EXAMPLE: List of endpoints should be sorted by name in descending order");
    /* SCENARIO
      Given 3 endpoint instance
        Name |
        Foo1
        Foo2
        Foo3
      When the sort by is set to Name (descending)
      Then the instances should be listed in reverse order
    */

    test.todo("EXAMPLE: List of endpoints should be sorted by latest heartbeat in ascending order");
    test.todo("EXAMPLE: List of endpoints should be sorted by latest heartbeat in descending order");
    /* SCENARIO
      Same again for Latest heartbeat
    */

    test.todo("EXAMPLE: Sort by should be persisted on page refresh and across tabs");
    /* SCENARIO
      Given the Sort By field has been changed
      When the page is refreshed
      Then the Sort By field retains its value
      And the Sort By field has the same value on all other Endpoint Heartbeats tabs
    */
  });

  describe("RULE: Filtering endpoints by name should be possible", () => {
    /* SCENARIO
      Given 3 endpoint instances
        Name |
        Foo1
        Bar
        Foo2
      When the text "foo" is entered into the filter box
      Then Foo1 should be shown
       And Foo2 should be shown
      And Bar should not be shown
    */
    test("EXAMPLE: When the filter string matches a subset of endpoint names, only those endpoints should be displayed", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.hasHeartbeatsEndpoints([
          { ...healthyEndpointTemplate, id: "Foo1", name: "Foo1", host_display_name: "Foo1.Hostname" },
          { ...healthyEndpointTemplate, id: "Bar", name: "Bar", host_display_name: "Bar.Hostname" },
          { ...healthyEndpointTemplate, id: "Foo2", name: "Foo2", host_display_name: "Foo2.Hostname" },
        ])
      );
      await driver.goTo("heartbeats/healthy");

      await setHeartbeatFilter("Foo");

      const visibleEndpoints = await getAllHeartbeatEndpointRecords();
      const unexpectedEndpoints = visibleEndpoints.filter((e) => e.name !== "Foo1" && e.name !== "Foo2");

      expect(unexpectedEndpoints.length).toBe(0);
      expect(visibleEndpoints.length).toBe(2);
    });

    /* SCENARIO
      Given 3 endpoint instances
        Name |
        Foo1
        Foo2
        Foo3
      When the text "bar" is entered into the filter box
      Then Foo1 should not be shown
       And Foo2 should not be shown
      And Foo3 should not be shown
    */
    test("EXAMPLE: Filter string matches no endpoint names should display no endpoints", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);
      await driver.setUp(
        precondition.hasHeartbeatsEndpoints([
          { ...healthyEndpointTemplate, id: "Foo1", name: "Foo1", host_display_name: "Foo1.Hostname" },
          { ...healthyEndpointTemplate, id: "Foo2", name: "Foo2", host_display_name: "Foo2.Hostname" },
          { ...healthyEndpointTemplate, id: "Foo3", name: "Foo3", host_display_name: "Foo3.Hostname" },
        ])
      );
      await driver.goTo("heartbeats/healthy");

      await setHeartbeatFilter("bar");

      const visibleEndpoints = await getAllHeartbeatEndpointRecords();
      const unexpectedEndpoints = visibleEndpoints.filter((e) => e.name !== "Foo1" && e.name !== "Foo2" && e.name !== "Foo3");

      expect(unexpectedEndpoints.length).toBe(0);
      expect(visibleEndpoints.length).toBe(0);
    });

    /* SCENARIO
      Given the filter string is ""
      When the text "Foo" is entered into the filter control
      Then the list is filtered (see above)
      And the filter control on the Healthy Endpoints tab contains "Foo"
      And the filter control on the Unhealthy endpoints tab contains "Foo"
    */
    test("EXAMPLE: The filter string is persisted on page refresh and across tabs", async ({ driver }) => {
      await driver.setUp(precondition.serviceControlWithMonitoring);

      await driver.goTo("heartbeats/healthy");

      await setHeartbeatFilter("bar");

      await navigateToUnHealthyHeartbeats();

      expect(await getHeartbeatFilterValue()).toBe("bar");
    });
  });

  describe("RULE: A performance monitoring warning should be displayed", () => {
    test.todo("EXAMPLE: A warning should be displayed when the configuration screen is loaded");

    /* SCENARIO
      When the configuration screen is loaded
      Then a warning should be displayed about this being disconnected to performance monitoring
    */
  });
});
