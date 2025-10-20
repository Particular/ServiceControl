import { describe, expect, test } from "vitest";
import { Driver } from "../../test/driver";
import { makeDriverForTests } from "@component-test-utils";
import { setActivePinia, storeToRefs } from "pinia";
import { createTestingPinia } from "@pinia/testing";
import { EndpointsView } from "@/resources/EndpointView";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import * as precondition from "../../test/preconditions";
import { EndpointSettings } from "@/resources/EndpointSettings";
import { serviceControlWithHeartbeats } from "@/components/heartbeats/serviceControlWithHeartbeats";
import { EndpointStatus } from "@/resources/Heartbeat";
import { ColumnNames, useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { useEnvironmentAndVersionsStore } from "./EnvironmentAndVersionsStore";

describe("HeartbeatsStore tests", () => {
  async function setup(endpoints: EndpointsView[], endpointSettings: EndpointSettings[] = [{ name: "", track_instances: true }], preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    const driver = makeDriverForTests();

    await preSetup(driver);
    await driver.setUp(serviceControlWithHeartbeats);
    await driver.setUp(precondition.hasHeartbeatsEndpoints(endpoints, endpointSettings));

    useServiceControlUrls();

    setActivePinia(createTestingPinia({ stubActions: false }));
    await useEnvironmentAndVersionsStore().refresh();

    const store = useHeartbeatsStore();
    const storeRefs = storeToRefs(store);
    await store.refresh();

    return { driver, ...store, ...storeRefs };
  }

  test("no endpoints", async () => {
    const { filteredHealthyEndpoints, filteredUnhealthyEndpoints } = await setup([]);

    expect(filteredHealthyEndpoints.value.length).toBe(0);
    expect(filteredUnhealthyEndpoints.value.length).toBe(0);
  });

  describe("healthchecks total", () => {
    test("all heart beating", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { failedHeartbeatsCount } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica" }) },
        ],
        [{ name: "", track_instances: true }]
      );

      expect(failedHeartbeatsCount.value).toBe(0);
    });

    test("some not heart beating", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { failedHeartbeatsCount } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
        ],
        [{ name: "", track_instances: true }]
      );

      expect(failedHeartbeatsCount.value).toBe(2);
    });

    test("some not heart beating with no tracking", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { failedHeartbeatsCount } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
        ],
        [
          { name: "", track_instances: true },
          { name: "John", track_instances: false },
        ]
      );

      expect(failedHeartbeatsCount.value).toBe(2);
    });

    test("some not heart beating in same logical endpoint and tracking", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { failedHeartbeatsCount } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
        ],
        [{ name: "", track_instances: true }]
      );

      expect(failedHeartbeatsCount.value).toBe(3);
    });

    test("all instances muted", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { failedHeartbeatsCount } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
        ],
        [
          { name: "", track_instances: true },
          { name: "John", track_instances: false },
        ]
      );

      expect(failedHeartbeatsCount.value).toBe(1);
    });
  });

  describe("healthy endpoints", () => {
    describe("total number when tracking instances", () => {
      test("when all instances are heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: true,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { healthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false }) },
          ],
          [{ name: "", track_instances: true }]
        );

        expect(healthyEndpoints.value.length).toBe(1);
      });

      test("when some instances are not heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: true,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { healthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          ],
          [{ name: "", track_instances: true }]
        );

        expect(healthyEndpoints.value.length).toBe(0);
      });
    });

    describe("total number when not tracking instances", () => {
      test("when all instances are heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: true,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { healthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false }) },
          ],
          [{ name: "", track_instances: false }]
        );

        expect(healthyEndpoints.value.length).toBe(1);
      });

      test("when some instances are not heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: true,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { healthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          ],
          [{ name: "", track_instances: false }]
        );

        expect(healthyEndpoints.value.length).toBe(0);
      });
    });

    test("filter by name", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { filteredHealthyEndpoints, endpointFilterString } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "johnny" }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
        ],
        [{ name: "", track_instances: true }]
      );

      expect(filteredHealthyEndpoints.value.length).toBe(3);
      endpointFilterString.value = "John";
      expect(filteredHealthyEndpoints.value.length).toBe(2);
      endpointFilterString.value = "Oliver";
      expect(filteredHealthyEndpoints.value.length).toBe(1);
    });

    test("sort by", async () => {
      const defaultEndpointsView = <EndpointsView>{
        is_sending_heartbeats: true,
        id: "",
        name: "",
        monitor_heartbeat: true,
        host_display_name: "",
        heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
      };
      const { filteredHealthyEndpoints, sortByInstances } = await setup(
        [
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-10-01T00:00:00" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-10-01T00:00:00" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Anna", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-01-01T00:00:00" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Anna", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-01-01T00:00:00" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Anna", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-01-01T00:00:00" } }) },
          { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "2024-06-01T00:00:00" } }) },
        ],
        [
          { name: "", track_instances: true },
          { name: "John", track_instances: false },
        ]
      );

      const names = () => filteredHealthyEndpoints.value.map((value) => value.name);

      sortByInstances.value = { property: ColumnNames.Name, isAscending: true };
      expect(names()).toEqual(["Anna", "John", "Oliver"]);

      sortByInstances.value = { property: ColumnNames.Name, isAscending: false };
      expect(names()).toEqual(["Oliver", "John", "Anna"]);

      sortByInstances.value = { property: ColumnNames.LastHeartbeat, isAscending: true };
      expect(names()).toEqual(["Anna", "Oliver", "John"]);

      sortByInstances.value = { property: ColumnNames.LastHeartbeat, isAscending: false };
      expect(names()).toEqual(["John", "Oliver", "Anna"]);

      sortByInstances.value = { property: ColumnNames.Tracked, isAscending: true };
      expect(names()[0]).toBe("John");

      sortByInstances.value = { property: ColumnNames.Tracked, isAscending: false };
      expect(names()[2]).toBe("John");

      sortByInstances.value = { property: ColumnNames.InstancesTotal, isAscending: true };
      expect(names()[2]).toBe("Anna");

      sortByInstances.value = { property: ColumnNames.InstancesTotal, isAscending: false };
      expect(names()[0]).toBe("Anna");
    });
  });

  describe("unhealthy endpoints", () => {
    describe("total number when tracking instances", () => {
      test("when all instances are heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: false,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { unhealthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false }) },
          ],
          [{ name: "", track_instances: true }]
        );

        expect(unhealthyEndpoints.value.length).toBe(2);
      });

      test("when some instances are not heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: false,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { unhealthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          ],
          [{ name: "", track_instances: true }]
        );

        expect(unhealthyEndpoints.value.length).toBe(3);
      });
    });

    describe("total number when not tracking instances", () => {
      test("when all instances are heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: true,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { unhealthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false }) },
          ],
          [{ name: "", track_instances: false }]
        );

        expect(unhealthyEndpoints.value.length).toBe(2);
      });

      test("when some instances are not heart beating", async () => {
        const defaultEndpointsView = <EndpointsView>{
          is_sending_heartbeats: false,
          id: "",
          name: "",
          monitor_heartbeat: true,
          host_display_name: "",
          heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
        };
        const { unhealthyEndpoints } = await setup(
          [
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Henry" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "John", monitor_heartbeat: false }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Oliver", heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica" }) },
            { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ name: "Monica", monitor_heartbeat: false, heartbeat_information: { reported_status: EndpointStatus.Dead, last_report_at: "" } }) },
          ],
          [{ name: "", track_instances: false }]
        );

        expect(unhealthyEndpoints.value.length).toBe(3);
      });
    });
  });
});
