import { MockEndpointDynamicOptions, SetupFactoryOptions } from "../driver";
import { EndpointsView } from "@/resources/EndpointView";
import { healthyEndpointTemplate, unHealthyEndpointTemplate } from "../mocks/heartbeat-endpoint-template";
import { DefaultBodyType, PathParams, StrictRequest } from "msw";
import { EndpointSettings } from "@/resources/EndpointSettings";
import { getDefaultConfig } from "@/defaultConfig";

interface ChangeTracking {
  track_instances: boolean;
}

interface MonitorHeartbeat {
  monitor_heartbeat: boolean;
}

export const hasHeartbeatsEndpoints = (
  endpoints: EndpointsView[],
  endpointSettings = [
    {
      name: "",
      track_instances: true,
    },
  ]
) => {
  const endpointSettingsList: EndpointSettings[] = [...endpointSettings];

  const endpointsList = [...endpoints];

  return ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpointDynamic(`${getDefaultConfig().service_control_url}endpoints`, "get", () => {
      return Promise.resolve({
        body: endpointsList,
      });
    });

    driver.mockEndpointDynamic(`${getDefaultConfig().service_control_url}endpointssettings`, "get", () => {
      return Promise.resolve({
        body: endpointSettingsList,
      });
    });

    endpointsList.forEach((e) => {
      driver.mockEndpointDynamic(`${getDefaultConfig().service_control_url}endpoints/${e.id}`, "patch", async (_url: URL, _params: PathParams, request: StrictRequest<DefaultBodyType>) => {
        const requestBody = <MonitorHeartbeat>await request.json();

        e.monitor_heartbeat = requestBody.monitor_heartbeat;

        return Promise.resolve(<MockEndpointDynamicOptions>{
          status: 200,
        });
      });

      driver.mockEndpointDynamic(`${getDefaultConfig().service_control_url}endpointssettings/${e.id}`, "patch", async (url: URL, params: PathParams, request: StrictRequest<DefaultBodyType>) => {
        const requestBody = <ChangeTracking>await request.json();

        let endpointSettings = endpointSettingsList.find((setting) => setting.name === e.name);

        if (!endpointSettings) {
          endpointSettings = {
            name: e.name,
            track_instances: requestBody.track_instances,
          };
          endpointSettingsList.push(endpointSettings);
        } else {
          endpointSettings.track_instances = requestBody.track_instances;
        }

        return <MockEndpointDynamicOptions>{
          status: 200,
        };
      });
    });
  };
};

export const hasUnhealthyHeartbeatsEndpoints = (numberOfUnhealthyEndpoints: number, endpointNamePrefix: string) => {
  const unhealthyEndpoints = [];

  for (let i = 0; i < numberOfUnhealthyEndpoints; i++) {
    unhealthyEndpoints.push(<EndpointsView>{
      ...unHealthyEndpointTemplate,
      id: `${endpointNamePrefix}_${i}`,
      name: `${endpointNamePrefix}_${i}`,
    });
  }

  return hasHeartbeatsEndpoints(unhealthyEndpoints);
};

export const hasAnUnhealthyEndpoint = () => hasUnhealthyHeartbeatsEndpoints(1, "UnhealthyHeartbeatEndpoint");

export const hasUnhealthyEndpoints = (numberOfUnhealthyHeartbeats: number) => hasUnhealthyHeartbeatsEndpoints(numberOfUnhealthyHeartbeats, "UnhealthyHeartbeatEndpoint");

export const hasAnUnhealthyEndpointWithNamePrefix = (endpointNamePrefix: string) => hasUnhealthyHeartbeatsEndpoints(1, endpointNamePrefix);

export const hasHealthyHeartbeatsEndpoints = (numberOfHealthyEndpoints: number, endpointNamePrefix: string) => {
  const healthyEndpoints = [];

  for (let i = 0; i < numberOfHealthyEndpoints; i++) {
    healthyEndpoints.push(<EndpointsView>{
      ...healthyEndpointTemplate,
      id: `${endpointNamePrefix}_${i}`,
      name: `${endpointNamePrefix}_${i}`,
    });
  }
  return hasHeartbeatsEndpoints(healthyEndpoints);
};

export const hasAHealthyEndpoint = () => hasHealthyHeartbeatsEndpoints(1, "HealthyHeartbeatEndpoit");

export const hasHealthyEndpoints = (numberOfUnhealthyHeartbeats: number) => hasHealthyHeartbeatsEndpoints(numberOfUnhealthyHeartbeats, "HealthyHeartbeatEndpoint");

export const hasAHealthyEndpointWithNamePrefix = (endpointNamePrefix: string) => hasHealthyHeartbeatsEndpoints(1, endpointNamePrefix);

export const hasNoHeartbeatsEndpoints = hasHeartbeatsEndpoints([]);

export const hasAnUnhealthyUnMonitoredEndpoint = () => {
  return hasHeartbeatsEndpoints([
    <EndpointsView>{
      ...unHealthyEndpointTemplate,
      id: `Unhealthy_UnmonitoredEndpoint`,
      name: `Unhealthy_UnmonitoredEndpoint`,
      monitor_heartbeat: false,
    },
  ]);
};

export const hasAHealthyButUnMonitoredEndpoint = () => {
  return hasHeartbeatsEndpoints([
    <EndpointsView>{
      ...healthyEndpointTemplate,
      id: `Healthy_UnmonitoredEndpoint`,
      name: `Healthy_UnmonitoredEndpoint`,
      monitor_heartbeat: false,
    },
  ]);
};

export const HasHealthyAndUnHealthyNamedEndpoints = (numberOfHealthyEndpoints: number, numberOfUnhealthyEndpoints: number, endpointNamePrefix: string) => {
  const endpoints: EndpointsView[] = [];

  const repeat = (action: (count: number) => void, count: number) => {
    if (count) {
      repeat(action, count - 1);
      action(count);
    }
  };

  repeat(
    (count) =>
      endpoints.push(<EndpointsView>{
        ...healthyEndpointTemplate,
        id: `${endpointNamePrefix}_${count}`,
        name: `${endpointNamePrefix}_${count}`,
      }),
    numberOfHealthyEndpoints
  );

  repeat(
    (count) =>
      endpoints.push(<EndpointsView>{
        ...unHealthyEndpointTemplate,
        id: `${endpointNamePrefix}_${numberOfHealthyEndpoints + count}`,
        name: `${endpointNamePrefix}_${numberOfHealthyEndpoints + count}`,
      }),
    numberOfUnhealthyEndpoints
  );

  return hasHeartbeatsEndpoints(endpoints);
  // for (let i = 0; i < numberOfHealthyEndpoints; i++) {
  //   endpoints.push(<EndpointsView>{
  //     ...healthyEndpointTemplate,
  //     name: `${endpointNamePrefix}_${i}`,
  //   });
  // }

  // for (let i = 0; i < numberOfUnhealthyEndpoints; i++) {
  //   endpoints.push(<EndpointsView>{
  //     ...unHealthyEndpointTemplate,
  //     name: `${endpointNamePrefix}_${i}`,
  //   });
  // }
};

export const HasHealthyAndUnHealthyEndpoints = (numberOfHealthyEndpoints: number, numberOfUnhealthyEndpoints: number) => HasHealthyAndUnHealthyNamedEndpoints(numberOfHealthyEndpoints, numberOfUnhealthyEndpoints, "TestEndpoint");
