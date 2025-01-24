import { ref } from "vue";
import { useTypedFetchFromMonitoring, useIsMonitoringDisabled } from "./serviceServiceControlUrls";
import { monitoringConnectionState } from "./serviceServiceControl";
import { useGetExceptionGroups } from "./serviceMessageGroup";
import { type Endpoint, type GroupedEndpoint, type EndpointGroup, type EndpointDetails, type EndpointDetailsError } from "@/resources/MonitoringEndpoint";

/**
 * @returns the max number of segments in a array of endpoint object names
 */
export function useFindEndpointSegments(endpoints: Endpoint[]) {
  return endpoints.reduce(function (acc, cur) {
    return Math.max(acc, cur.name.split(".").length - 1);
  }, 0);
}

/**
 * @param historyPeriod - The history period value.  The default is (1)
 * @returns A array of monitoring endpoint objects
 */
export async function useGetAllMonitoredEndpoints(historyPeriod = 1) {
  let endpoints: Endpoint[] = [];
  if (!useIsMonitoringDisabled() && !monitoringConnectionState.unableToConnect) {
    try {
      const [, data] = await useTypedFetchFromMonitoring<Endpoint[]>(`monitored-endpoints?history=${historyPeriod}`);
      endpoints = data ?? [];
      await addEndpointsFromScSubscription(endpoints);
    } catch (error) {
      console.error(error);
    }
  } else {
    await addEndpointsFromScSubscription(endpoints);
  }
  return endpoints;
}

/**
 * @param filterString - The text entered by the user when filtering monitoring endpoints by name
 * @returns A filtered array of monitoring endpoint objects
 */
export function useFilterAllMonitoredEndpointsByName(endpoints: Endpoint[], filterString: string) {
  if (!filterString) {
    return endpoints;
  }
  return endpoints.filter((endpoint) => endpoint.name.toLowerCase().includes(filterString.toLowerCase()));
}

/**
 * @param endpoints - An array of endpoint objects from ServiceControl
 * @param numberOfSegments - The number of segments to group the array of endpoints by
 * @returns An array of grouped endpoint objects
 */
export function useGroupEndpoints(endpoints: Endpoint[], numberOfSegments: number): EndpointGroup[] {
  const groups = new Map<string, EndpointGroup>();
  for (const element of endpoints) {
    const grouping = parseEndpoint(element, numberOfSegments);

    const resultGroup = groups.get(grouping.groupName) ?? {
      group: grouping.groupName,
      endpoints: [],
    };
    resultGroup.endpoints.push(grouping);
    groups.set(grouping.groupName, resultGroup);
  }
  return [...groups.values()];
}

/**
 * @param endpointName - the endpoint name whose details is needed
 * @param The history period value.  The default is (1)
 * @returns The details of the endpoint
 */
export function useGetEndpointDetails(endpointName: string, historyPeriod = 1) {
  const data = ref<EndpointDetails | EndpointDetailsError | null>(null);
  return {
    data,
    refresh: async () => {
      if (!useIsMonitoringDisabled() && !monitoringConnectionState.unableToConnect) {
        try {
          const [, details] = await useTypedFetchFromMonitoring<EndpointDetails>(`${`monitored-endpoints`}/${endpointName}?history=${historyPeriod}`);
          data.value = details!;
        } catch (error) {
          console.error(error);
          if (error instanceof Error) {
            data.value = { error: error.message } as EndpointDetailsError;
          }
        }
      }
    },
  };
}

/**
 * @returns The count of disconnected endpoint
 */
export async function useGetDisconnectedEndpointCount() {
  try {
    const [, count] = await useTypedFetchFromMonitoring<number>(`${`monitored-endpoints`}/disconnected`);
    return count ?? 0;
  } catch (error) {
    console.error(error);
  }
  return 0;
}

async function addEndpointsFromScSubscription(endpoints: Endpoint[]) {
  const exceptionGroups = await useGetExceptionGroups("Endpoint Name");

  //Squash and add to existing monitored endpoints
  if (exceptionGroups.length > 0) {
    //sort the exceptionGroups array by name - case sensitive
    exceptionGroups.sort((a, b) => (a.title > b.title ? 1 : a.title < b.title ? -1 : 0)); //desc
    exceptionGroups
      .filter((exceptionGroup) => exceptionGroup.operation_status !== "ArchiveCompleted")
      .forEach((exceptionGroup) => {
        const monitoredEndpoint = endpoints.find((item) => item.name === exceptionGroup.title);
        if (monitoredEndpoint) {
          monitoredEndpoint.serviceControlId = exceptionGroup.id;
          monitoredEndpoint.errorCount = exceptionGroup.count;
        }
      });
  }
}

function parseEndpoint(endpoint: Endpoint, maxGroupSegments: number) {
  if (maxGroupSegments === 0) {
    return {
      groupName: "Ungrouped",
      shortName: endpoint.name,
      endpoint: endpoint,
    };
  }

  const segments = endpoint.name.split(".");
  const groupSegments = segments.slice(0, maxGroupSegments);
  const endpointSegments = segments.slice(maxGroupSegments);
  if (endpointSegments.length === 0) {
    // the endpoint's name is shorter than the group size
    return parseEndpoint(endpoint, maxGroupSegments - 1);
  }

  return {
    groupName: groupSegments.join("."),
    shortName: endpointSegments.join("."),
    endpoint,
  } as GroupedEndpoint;
}
