import { heartbeatEndpointTemplate } from "../mocks/heartbeat-endpoint-template";
import { SetupFactoryOptions } from "../driver";

export const heartbeatsEndpointsNamed =
  (endpointNames: string[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const response = endpointNames.map((name) => {
      return { ...heartbeatEndpointTemplate, name: name };
    });

    const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
    driver.mockEndpoint(`${serviceControlInstanceUrl}endpoints`, {
      body: response,
    });
    return response;
  };

export const hasNoHeartbeatsEndpoints = heartbeatsEndpointsNamed([]);
