import { describe, expect, test } from "vitest";
import { Driver } from "../../test/driver";
import { makeDriverForTests } from "@component-test-utils";
import { setActivePinia, storeToRefs } from "pinia";
import { createTestingPinia } from "@pinia/testing";
import { ColumnNames, useHeartbeatInstancesStore } from "@/stores/HeartbeatInstancesStore";
import { EndpointsView } from "@/resources/EndpointView";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import * as precondition from "../../test/preconditions";
import { EndpointSettings } from "@/resources/EndpointSettings";
import { serviceControlWithHeartbeats } from "@/components/heartbeats/serviceControlWithHeartbeats";
import { EndpointStatus } from "@/resources/Heartbeat";
import { useEnvironmentAndVersionsStore } from "./EnvironmentAndVersionsStore";

describe("HeartbeatInstancesStore tests", () => {
  async function setup(endpoints: EndpointsView[], endpointSettings: EndpointSettings[], preSetup: (driver: Driver) => Promise<void> = () => Promise.resolve()) {
    const driver = makeDriverForTests();

    await preSetup(driver);
    await driver.setUp(serviceControlWithHeartbeats);
    await driver.setUp(precondition.hasEndpointSettings(endpointSettings));
    await driver.setUp(precondition.hasHeartbeatsEndpoints(endpoints));

    useServiceControlUrls();

    setActivePinia(createTestingPinia({ stubActions: false }));
    await useEnvironmentAndVersionsStore().refresh();

    const store = useHeartbeatInstancesStore();
    const refs = storeToRefs(store);

    await store.refresh();

    return { driver, ...refs };
  }

  test("no endpoints", async () => {
    const { filteredInstances } = await setup([], []);

    expect(filteredInstances.value.length).toBe(0);
  });

  test("filter by name", async () => {
    const defaultEndpointsView = <EndpointsView>{
      is_sending_heartbeats: false,
      id: "",
      name: "",
      monitor_heartbeat: false,
      host_display_name: "",
      heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
    };
    const { filteredInstances, instanceFilterString } = await setup(
      [
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "John" }) },
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "johnny" }) },
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "Oliver" }) },
      ],
      [{ name: "", track_instances: true }]
    );

    expect(filteredInstances.value.length).toBe(3);
    instanceFilterString.value = "John";
    expect(filteredInstances.value.length).toBe(2);
    instanceFilterString.value = "Oliver";
    expect(filteredInstances.value.length).toBe(1);
  });

  test("sort by", async () => {
    const defaultEndpointsView = <EndpointsView>{
      is_sending_heartbeats: false,
      id: "",
      name: "",
      monitor_heartbeat: false,
      host_display_name: "",
      heartbeat_information: { reported_status: EndpointStatus.Alive, last_report_at: "" },
    };
    const { filteredInstances, sortByInstances } = await setup(
      [
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "John", heartbeat_information: { last_report_at: "2024-10-01T00:00:00" } }) },
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "Anna", heartbeat_information: { last_report_at: "2024-01-01T00:00:00" } }) },
        { ...defaultEndpointsView, ...(<Partial<EndpointsView>>{ host_display_name: "Oliver", heartbeat_information: { last_report_at: "2024-06-01T00:00:00" } }) },
      ],
      [{ name: "", track_instances: true }]
    );

    const names = () => filteredInstances.value.map((value) => value.host_display_name);
    sortByInstances.value = { property: ColumnNames.InstanceName, isAscending: true };
    expect(names()).toEqual(["Anna", "John", "Oliver"]);

    sortByInstances.value = { property: ColumnNames.InstanceName, isAscending: false };
    expect(names()).toEqual(["Oliver", "John", "Anna"]);

    sortByInstances.value = { property: ColumnNames.LastHeartbeat, isAscending: true };
    expect(names()).toEqual(["Anna", "Oliver", "John"]);

    sortByInstances.value = { property: ColumnNames.LastHeartbeat, isAscending: false };
    expect(names()).toEqual(["John", "Oliver", "Anna"]);
  });
});
