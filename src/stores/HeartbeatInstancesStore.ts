import { useDeleteFromServiceControl, usePatchToServiceControl } from "@/composables/serviceServiceControlUrls";
import { acceptHMRUpdate, defineStore, storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";
import moment from "moment";
import type { SortInfo } from "@/components/SortInfo";
import { type GroupPropertyType, SortDirection } from "@/resources/SortOptions";
import getSortFunction from "@/components/getSortFunction";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { EndpointsView } from "@/resources/EndpointView";

export enum ColumnNames {
  InstanceName = "name",
  LastHeartbeat = "latestHeartbeat",
  MuteToggle = "toggleMuteAlerts",
}

const columnSortings = new Map<string, (endpoint: EndpointsView) => GroupPropertyType>([
  [ColumnNames.InstanceName, (endpoint) => endpoint.host_display_name],
  [ColumnNames.LastHeartbeat, (endpoint) => moment.utc(endpoint.heartbeat_information?.last_report_at ?? "1975-01-01T00:00:00")],
  [ColumnNames.MuteToggle, (endpoint) => !endpoint.monitor_heartbeat],
]);

export const useHeartbeatInstancesStore = defineStore("HeartbeatInstancesStore", () => {
  const instanceFilterString = ref("");
  const store = useHeartbeatsStore();
  const { endpointInstances } = storeToRefs(store);
  const sortByInstances = ref<SortInfo>({
    property: ColumnNames.InstanceName,
    isAscending: true,
  });

  const sortedInstances = computed<EndpointsView[]>(() => endpointInstances.value.sort(getSortFunction(columnSortings.get(sortByInstances.value.property), sortByInstances.value.isAscending ? SortDirection.Ascending : SortDirection.Descending)));
  const filteredInstances = computed<EndpointsView[]>(() => sortedInstances.value.filter((instance) => !instanceFilterString.value || instance.host_display_name.toLowerCase().includes(instanceFilterString.value.toLowerCase())));

  const refresh = () => store.refresh();

  watch(instanceFilterString, (newValue) => {
    setInstanceFilterString(newValue);
  });

  function setInstanceFilterString(filter: string) {
    instanceFilterString.value = filter;
  }

  async function deleteEndpointInstance(endpoint: EndpointsView) {
    await useDeleteFromServiceControl(`endpoints/${endpoint.id}`);
    await store.refresh();
  }

  async function toggleEndpointMonitor(endpoints: EndpointsView[]) {
    await Promise.all(endpoints.map((endpoint) => usePatchToServiceControl(`endpoints/${endpoint.id}`, { monitor_heartbeat: !endpoint.monitor_heartbeat })));
    await store.refresh();
  }

  return {
    refresh,
    sortedInstances,
    filteredInstances,
    instanceFilterString,
    deleteEndpointInstance,
    toggleEndpointMonitor,
    sortByInstances,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useHeartbeatInstancesStore, import.meta.hot));
}

export type HeartbeatInstancesStore = ReturnType<typeof useHeartbeatInstancesStore>;
