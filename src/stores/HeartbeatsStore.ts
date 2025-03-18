import { usePatchToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import useAutoRefresh from "@/composables/autoRefresh";
import { EndpointStatus, LogicalEndpoint } from "@/resources/Heartbeat";
import moment from "moment";
import { SortDirection, type GroupPropertyType } from "@/resources/SortOptions";
import getSortFunction from "@/components/getSortFunction";
import { EndpointsView } from "@/resources/EndpointView";
import endpointSettingsClient from "@/components/heartbeats/endpointSettingsClient";
import type { SortInfo } from "@/components/SortInfo";
import { EndpointSettings } from "@/resources/EndpointSettings";

export enum ColumnNames {
  Name = "name",
  InstancesDown = "instancesDown",
  InstancesTotal = "instancesTotal",
  LastHeartbeat = "latestHeartbeat",
  Muted = "muted",
  Tracked = "instancesTracked",
  TrackToggle = "toggleInstancesTracked",
}

export enum MutedType {
  None = 0,
  Some = 1,
  All = 2,
}

const columnSortings = new Map<string, (endpoint: LogicalEndpoint) => GroupPropertyType>([
  [ColumnNames.Name, (endpoint) => endpoint.name],
  [ColumnNames.InstancesDown, (endpoint) => endpoint.alive_count - endpoint.down_count],
  [ColumnNames.InstancesTotal, (endpoint) => endpoint.alive_count + endpoint.down_count],
  [ColumnNames.LastHeartbeat, (endpoint) => moment.utc(endpoint.heartbeat_information?.last_report_at ?? "1975-01-01T00:00:00")],
  [
    ColumnNames.Muted,
    (endpoint) => {
      switch (endpoint.muted_count) {
        case 0:
          return MutedType.None;
        case endpoint.alive_count + endpoint.down_count:
          return MutedType.All;
        default:
          return MutedType.Some;
      }
    },
  ],
  [ColumnNames.Tracked, (endpoint) => endpoint.track_instances],
  [ColumnNames.TrackToggle, (endpoint) => endpoint.track_instances],
]);

export const useHeartbeatsStore = defineStore("HeartbeatsStore", () => {
  const sortByInstances = ref<SortInfo>({
    property: ColumnNames.Name,
    isAscending: true,
  });

  const defaultTrackingInstancesValue = ref(endpointSettingsClient.defaultEndpointSettingsValue().track_instances);
  const endpointFilterString = ref("");
  const itemsPerPage = ref(20);
  const endpointInstances = ref<EndpointsView[]>([]);
  const settings = ref<EndpointSettings[]>([]);
  const sortedEndpoints = computed<LogicalEndpoint[]>(() =>
    mapEndpointsToLogical(endpointInstances.value, settings.value).sort(getSortFunction(columnSortings.get(sortByInstances.value.property), sortByInstances.value.isAscending ? SortDirection.Ascending : SortDirection.Descending))
  );
  const filteredEndpoints = computed<LogicalEndpoint[]>(() => sortedEndpoints.value.filter((endpoint) => !endpointFilterString.value || endpoint.name.toLowerCase().includes(endpointFilterString.value.toLowerCase())));
  const healthyEndpoints = computed<LogicalEndpoint[]>(() =>
    sortedEndpoints.value.filter(function (endpoint) {
      return endpoint.monitor_heartbeat && endpoint.heartbeat_information?.reported_status === EndpointStatus.Alive && ((endpoint.track_instances && endpoint.down_count === 0) || (!endpoint.track_instances && endpoint.alive_count > 0));
    })
  );
  const filteredHealthyEndpoints = computed<LogicalEndpoint[]>(() => healthyEndpoints.value.filter((endpoint) => !endpointFilterString.value || endpoint.name.toLowerCase().includes(endpointFilterString.value.toLowerCase())));
  const unhealthyEndpoints = computed<LogicalEndpoint[]>(() =>
    sortedEndpoints.value.filter(function (endpoint) {
      return !endpoint.monitor_heartbeat || endpoint.heartbeat_information?.reported_status === EndpointStatus.Dead || (endpoint.track_instances && endpoint.down_count > 0) || (!endpoint.track_instances && endpoint.alive_count === 0);
    })
  );
  const filteredUnhealthyEndpoints = computed<LogicalEndpoint[]>(() => unhealthyEndpoints.value.filter((endpoint) => !endpointFilterString.value || endpoint.name.toLowerCase().includes(endpointFilterString.value.toLowerCase())));
  const failedHeartbeatsCount = computed(() => {
    let counter = 0;

    for (const logical of sortedEndpoints.value) {
      const endpointInstancesThatAreNotMuted = endpointInstances.value.filter((instance) => instance.name === logical.name && instance.monitor_heartbeat);

      if (logical.track_instances) {
        if (endpointInstancesThatAreNotMuted.some((instance) => instance.heartbeat_information?.reported_status !== EndpointStatus.Alive)) {
          counter++;
        }
      } else {
        if (!endpointInstancesThatAreNotMuted.some((instance) => instance.heartbeat_information?.reported_status === EndpointStatus.Alive)) {
          counter++;
        }
      }
    }

    return counter;
  });
  watch(endpointFilterString, (newValue) => {
    setEndpointFilterString(newValue);
  });

  const dataRetriever = useAutoRefresh(async () => {
    try {
      const [[, data], data2] = await Promise.all([useTypedFetchFromServiceControl<EndpointsView[]>("endpoints"), endpointSettingsClient.endpointSettings()]);
      endpointInstances.value = data;
      settings.value = data2;
      defaultTrackingInstancesValue.value = data2.find((value) => value.name === "")!.track_instances;
    } catch (e) {
      endpointInstances.value = settings.value = [];
      throw e;
    }
  }, 5000);

  async function updateEndpointSettings(endpoints: Pick<LogicalEndpoint, "name" | "track_instances">[]) {
    await Promise.all(endpoints.map((endpoint) => usePatchToServiceControl(`endpointssettings/${endpoint.name}`, { track_instances: !endpoint.track_instances })));
    await refresh();
  }

  function instanceDisplayText(endpoint: LogicalEndpoint) {
    const total = endpoint.alive_count + endpoint.down_count;

    if (endpoint.track_instances) {
      return `${endpoint.alive_count}/${total}`;
    } else {
      return `${endpoint.alive_count}`;
    }
  }

  function setEndpointFilterString(filter: string) {
    endpointFilterString.value = filter;
  }

  function setItemsPerPage(value: number) {
    itemsPerPage.value = value;
  }

  function mapEndpointsToLogical(endpoints: EndpointsView[], settings: EndpointSettings[]): LogicalEndpoint[] {
    const logicalNames = [...new Set(endpoints.map((endpoint) => endpoint.name))];

    return logicalNames.map((endpointName) => {
      const endpointInstances = endpoints.filter((endpoint) => endpoint.name === endpointName);
      const aliveList = endpointInstances.filter((endpoint) => endpoint.heartbeat_information && endpoint.heartbeat_information.reported_status === EndpointStatus.Alive);

      const aliveCount = aliveList.length;
      const downCount = endpointInstances.length - aliveCount;

      return {
        id: endpointName, //need this to be consistent between data refreshes for UI purposes, so using name rather than an id from one of the instances
        name: endpointName,
        alive_count: aliveCount,
        down_count: downCount,
        muted_count: endpointInstances.filter((endpoint) => !endpoint.monitor_heartbeat).length,
        track_instances: settings.find((value) => value.name === endpointName)?.track_instances ?? defaultTrackingInstancesValue.value,
        heartbeat_information: {
          reported_status: aliveCount > 0 ? EndpointStatus.Alive : EndpointStatus.Dead,
          last_report_at: endpointInstances.reduce((previousMax: EndpointsView | null, endpoint: EndpointsView) => {
            if (endpoint.heartbeat_information) {
              if (previousMax) {
                return moment.utc(endpoint.heartbeat_information.last_report_at) > moment.utc(previousMax.heartbeat_information!.last_report_at) ? endpoint : previousMax;
              }
              return endpoint;
            }
            return previousMax;
          }, null)?.heartbeat_information?.last_report_at,
        },
        monitor_heartbeat: endpointInstances.every((endpoint) => endpoint.monitor_heartbeat),
      } as LogicalEndpoint;
    });
  }

  const refresh = dataRetriever.executeAndResetTimer;

  return {
    refresh,
    defaultTrackingInstancesValue,
    updateEndpointSettings,
    sortedEndpoints,
    filteredEndpoints,
    endpointInstances,
    healthyEndpoints,
    filteredHealthyEndpoints,
    unhealthyEndpoints,
    filteredUnhealthyEndpoints,
    failedHeartbeatsCount,
    instanceDisplayText,
    sortByInstances,
    endpointFilterString,
    itemsPerPage,
    setItemsPerPage,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useHeartbeatsStore, import.meta.hot));
}

export type HeartbeatsStore = ReturnType<typeof useHeartbeatsStore>;
