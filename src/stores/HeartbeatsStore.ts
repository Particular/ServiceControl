import { useDeleteFromServiceControl, usePatchToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import useAutoRefresh from "@/composables/autoRefresh";
import { Endpoint, EndpointStatus } from "@/resources/Heartbeat";
import moment from "moment";
import SortOptions, { SortDirection } from "@/resources/SortOptions";
import { getSortFunction } from "@/components/OrderBy.vue";
import { useCookies } from "vue3-cookies";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";

export enum DisplayType {
  Instances = "Endpoint Instances",
  Logical = "Logical Endpoints",
}

function mapEndpointsToLogical(endpoints: Endpoint[]) {
  const logicalNames = [...new Set(endpoints.map((endpoint) => endpoint.name))];
  return logicalNames.map((endpointName) => {
    const logicalList = endpoints.filter((endpoint) => endpoint.name === endpointName);
    const aliveList = logicalList.filter((endpoint) => endpoint.monitor_heartbeat && endpoint.heartbeat_information && endpoint.heartbeat_information.reported_status === EndpointStatus.Alive);

    var aliveCount = aliveList.length;
    var downCount = logicalList.filter((endpoint) => endpoint.monitor_heartbeat).length - aliveCount;

    return {
      name: endpointName,
      aliveCount: aliveCount,
      downCount: downCount,
      heartbeat_information: {
        reported_status: aliveCount > 0 ? EndpointStatus.Alive : EndpointStatus.Dead,
        last_report_at: logicalList.reduce((previousMax: Endpoint | null, endpoint: Endpoint) => {
          if (endpoint.heartbeat_information) {
            if (previousMax) {
              return moment.utc(endpoint.heartbeat_information.last_report_at) > moment.utc(previousMax.heartbeat_information!.last_report_at) ? endpoint : previousMax;
            }
            return endpoint;
          }
          return previousMax;
        }, null)?.heartbeat_information?.last_report_at,
      },
      monitor_heartbeat: logicalList.some((endpoint) => endpoint.monitor_heartbeat),
    } as Endpoint;
  });
}

export const sortOptions: SortOptions<Endpoint>[] = [
  {
    description: "Name",
    selector: (group) => group.name,
    icon: "bi-sort-alpha-",
  },
  {
    description: "Latest heartbeat",
    selector: (group) => group.heartbeat_information?.last_report_at ?? "",
    icon: "bi-sort-",
  },
];

export const useHeartbeatsStore = defineStore("HeartbeatsStore", () => {
  const cookies = useCookies().cookies;

  const selectedDisplay = ref(cookies.get("heartbeats_display_type") ?? DisplayType.Instances);
  const selectedSort = ref<SortOptions<Endpoint>>(sortOptions[0]);
  const filterString = ref("");
  const endpoints = ref<Endpoint[]>([]);
  const sortedEndpoints = computed<Endpoint[]>(() =>
    (selectedDisplay.value === DisplayType.Instances ? [...endpoints.value] : mapEndpointsToLogical(endpoints.value)).sort(selectedSort.value.sort ?? getSortFunction(sortOptions[0].selector, SortDirection.Ascending))
  );
  const activeEndpoints = computed<Endpoint[]>(() => sortedEndpoints.value.filter((endpoint) => endpoint.monitor_heartbeat && endpoint.heartbeat_information && endpoint.heartbeat_information.reported_status === EndpointStatus.Alive));
  const filteredActiveEndpoints = computed<Endpoint[]>(() => activeEndpoints.value.filter((endpoint) => !filterString.value || endpoint.name.toLowerCase().includes(filterString.value.toLowerCase())));
  const inactiveEndpoints = computed<Endpoint[]>(() => sortedEndpoints.value.filter((endpoint) => endpoint.monitor_heartbeat && (!endpoint.heartbeat_information || endpoint.heartbeat_information.reported_status !== EndpointStatus.Alive)));
  const filteredInactiveEndpoints = computed<Endpoint[]>(() => inactiveEndpoints.value.filter((endpoint) => !filterString.value || endpoint.name.toLowerCase().includes(filterString.value.toLowerCase())));
  const failedHeartbeatsCount = computed(() => inactiveEndpoints.value.length);

  watch(filterString, (newValue) => {
    setFilterString(newValue);
  });

  const dataRetriever = useAutoRefresh(async () => {
    try {
      const [, data] = await useTypedFetchFromServiceControl<Endpoint[]>("endpoints");
      endpoints.value = data;
    } catch (e) {
      endpoints.value = [];
      throw e;
    }
  }, 5000);

  function endpointDisplayName(endpoint: Endpoint) {
    if (selectedDisplay.value === DisplayType.Logical) {
      if (endpoint.aliveCount > 0) {
        return `${endpoint.name} (${endpoint.aliveCount} instance${endpoint.aliveCount > 1 ? "s" : ""})`;
      }

      return `${endpoint.name} (0 out of ${endpoint.downCount} previous instance${endpoint.downCount > 1 ? "s" : ""} reporting)`;
    }

    return `${endpoint.name}@${endpoint.host_display_name}`;
  }

  function setSelectedDisplay(displayType: DisplayType) {
    cookies.set("heartbeats_display_type", displayType);
    selectedDisplay.value = displayType;
  }

  function setSelectedSort(sort: SortOptions<Endpoint>) {
    //sort value is set/retrieved from cookies in the OrderBy control
    selectedSort.value = sort;
  }

  function setFilterString(filter: string) {
    filterString.value = filter;
  }

  async function deleteEndpoint(endpoint: Endpoint) {
    async function performDelete() {
      useShowToast(TYPE.INFO, "Info", "Removing Endpoint");
      await useDeleteFromServiceControl(`endpoints/${endpoint.id}`);
      endpoints.value = endpoints.value.filter((ep) => ep.id !== endpoint.id);
      useShowToast(TYPE.SUCCESS, "Success", "Endpoint removed");
    }
    await dataRetriever.executeAndResetTimer(performDelete);
  }

  function toggleEndpointMonitor(endpoint: Endpoint) {
    usePatchToServiceControl(`endpoints/${endpoint.id}`, { monitor_heartbeat: !endpoint.monitor_heartbeat });
  }

  dataRetriever.executeAndResetTimer();

  return {
    endpoints,
    activeEndpoints,
    filteredActiveEndpoints,
    inactiveEndpoints,
    filteredInactiveEndpoints,
    failedHeartbeatsCount,
    endpointDisplayName,
    selectedDisplay,
    setSelectedDisplay,
    selectedSort,
    setSelectedSort,
    filterString,
    setFilterString,
    deleteEndpoint,
    toggleEndpointMonitor,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useHeartbeatsStore, import.meta.hot));
}

export type HeartbeatsStore = ReturnType<typeof useHeartbeatsStore>;
