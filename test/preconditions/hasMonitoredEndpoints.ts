import hasEndpointsResponse from "../mocks/monitored-endpoints.json";
import hasNoEndpoints from "../mocks/monitored-endpoints-empty.json";
import monitoredEndpointTemplate from "../mocks/monitored-endpoint-template";

import { SetupFactoryOptions } from "../driver";
import { Endpoint } from "@/resources/MonitoringEndpoint";

export const hasMonitoredEndpoints = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
    body: hasEndpointsResponse,
  });
  return hasEndpointsResponse;
};

export const hasNoMonitoredEndpoints = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
    body: hasNoEndpoints,
  });
  return hasNoEndpoints;
};

export const monitoredEndpointsNamed =
  (endpointNames: string[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const response = endpointNames.map((name) => {
      return { ...monitoredEndpointTemplate, name: name };
    });

    const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
    driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
      body: response,
    });
    return response;
  };

export const hasMonitoredEndpointsList =
  (monitoringEndpointTemplates: Endpoint[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
    driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
      body: monitoringEndpointTemplates,
    });
    return monitoringEndpointTemplates;
  };
