import { defineStore, acceptHMRUpdate } from "pinia";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import * as MonitoringEndpoints from "../composables/serviceMonitoringEndpoints";
import { useMonitoringHistoryPeriodStore } from "./MonitoringHistoryPeriodStore";
import type { EndpointGroup, Endpoint, GroupedEndpoint } from "@/resources/MonitoringEndpoint";
import type { SortInfo } from "@/components/SortInfo";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

export const useMonitoringStore = defineStore("MonitoringStore", () => {
  const historyPeriodStore = useMonitoringHistoryPeriodStore();

  const route = useRoute();
  const router = useRouter();
  const { store: connectionStore } = useConnectionsAndStatsAutoRefresh();

  //STORE STATE CONSTANTS
  const grouping = ref({
    groupedEndpoints: [] as EndpointGroup[],
    groupSegments: 0,
    selectedGrouping: 0,
  });

  const sortBy = ref<SortInfo>({
    property: "name",
    isAscending: true,
  });

  const endpointList = ref<Endpoint[]>([]);
  const disconnectedEndpointCount = ref(0);
  const filterString = ref("");
  const endpointListCount = computed<number>(() => endpointList.value.length);
  const endpointListIsEmpty = computed<boolean>(() => endpointListCount.value === 0);
  const endpointListIsGrouped = computed<boolean>(() => grouping.value.selectedGrouping !== 0);
  const getEndpointList = computed<Endpoint[]>(() => (filterString.value !== "" ? MonitoringEndpoints.useFilterAllMonitoredEndpointsByName(endpointList.value, filterString.value) : endpointList.value));

  watch(sortBy, async () => await updateEndpointList(), { deep: true });
  watch(filterString, async (newValue) => {
    await updateFilterString(newValue);
  });

  //STORE ACTIONS
  async function updateFilterString(filter: string | null = null) {
    filterString.value = filter ?? route.query.filter?.toString() ?? "";

    if (filterString.value === "") {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { filter, ...withoutFilter } = route.query;
      await router.replace({ query: withoutFilter }); // Update or add filter query parameter to url
    } else {
      await router.replace({ query: { ...route.query, filter: filterString.value } }); // Update or add filter query parameter to url
    }
    updateGroupedEndpoints();
  }

  async function updateEndpointList() {
    if (connectionStore.monitoringConnectionState.unableToConnect) {
      endpointList.value = [];
    } else {
      endpointList.value = await MonitoringEndpoints.getAllMonitoredEndpoints(historyPeriodStore.historyPeriod.pVal);
    }
    if (!endpointListIsEmpty.value) {
      updateGroupSegments();
      if (endpointListIsGrouped.value) {
        updateGroupedEndpoints();
      } else {
        sortEndpointList();
      }
    }
  }

  function updateSelectedGrouping(groupSize: number) {
    grouping.value.selectedGrouping = groupSize;
    if (groupSize === 0) {
      sortEndpointList();
    } else {
      updateGroupedEndpoints();
    }
  }

  function updateGroupSegments() {
    grouping.value.groupSegments = MonitoringEndpoints.useFindEndpointSegments(endpointList.value);
  }

  function updateGroupedEndpoints() {
    grouping.value.groupedEndpoints = MonitoringEndpoints.useGroupEndpoints(getEndpointList.value, grouping.value.selectedGrouping);
    sortGroupedEndpointList();
  }

  function sortEndpointList() {
    const comparator = (() => {
      if (sortBy.value.property === "name") {
        return (a: Endpoint, b: Endpoint) => (sortBy.value.isAscending ? a.name.localeCompare(b.name) : b.name.localeCompare(a.name));
      } else {
        return (a: Endpoint, b: Endpoint) => {
          const propertyA = a.metrics[sortBy.value.property].average;
          const propertyB = b.metrics[sortBy.value.property].average;

          return sortBy.value.isAscending ? propertyA - propertyB : propertyB - propertyA;
        };
      }
    })();

    endpointList.value.sort(comparator);
  }

  function sortGroupedEndpointList() {
    let comparator;
    const endpointShortNameComparator = (a: GroupedEndpoint, b: GroupedEndpoint) => {
      return sortBy.value.isAscending ? a.shortName.localeCompare(b.shortName) : b.shortName.localeCompare(a.shortName);
    };

    if (sortBy.value.property === "name") {
      comparator = (a: EndpointGroup, b: EndpointGroup) => {
        const groupNameA = a.group;
        const groupNameB = b.group;
        const endpointListGroupA = a.endpoints;
        const endpointListGroupB = b.endpoints;

        // Sort each group's endpoints before sorting the group name
        endpointListGroupA.sort(endpointShortNameComparator);
        endpointListGroupB.sort(endpointShortNameComparator);

        return sortBy.value.isAscending ? groupNameA.localeCompare(groupNameB) : groupNameB.localeCompare(groupNameA);
      };
    }
    // TODO: Determine how sorting should be handled for columns other than endpoint name

    if (grouping.value.groupedEndpoints.length > 1) {
      grouping.value.groupedEndpoints.sort(comparator);
    } else if (grouping.value.groupedEndpoints.length === 1) {
      grouping.value.groupedEndpoints[0].endpoints.sort(endpointShortNameComparator);
    }
  }

  return {
    //state
    grouping,
    endpointList,
    disconnectedEndpointCount,
    filterString,
    sortBy,

    //getters
    endpointListCount,
    endpointListIsEmpty,
    endpointListIsGrouped,
    getEndpointList,

    //actions
    updateSelectedGrouping,
    updateEndpointList,
    updateFilterString,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useMonitoringStore, import.meta.hot));
}

export type MonitoringStore = ReturnType<typeof useMonitoringStore>;
