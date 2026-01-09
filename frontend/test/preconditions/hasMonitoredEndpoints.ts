import { monitoredEndpointTemplate, noMonitoredEndpoints } from "../mocks/monitored-endpoint-template";

import { SetupFactoryOptions } from "../driver";
import { Endpoint } from "@/resources/MonitoringEndpoint";
import { getDefaultConfig } from "@/defaultConfig";

export const hasNoMonitoredEndpoints = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = getDefaultConfig().monitoring_url;
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
    body: noMonitoredEndpoints,
  });
  return noMonitoredEndpoints;
};

export const monitoredEndpointsNamed =
  (endpointNames: string[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const response = endpointNames.map((name) => {
      return { ...monitoredEndpointTemplate, name: name };
    });

    const monitoringInstanceUrl = getDefaultConfig().monitoring_url;
    driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
      body: response,
    });
    return response;
  };

export const hasMonitoredEndpointsList =
  (monitoringEndpointTemplates: Endpoint[]) =>
  ({ driver }: SetupFactoryOptions) => {
    const monitoringInstanceUrl = getDefaultConfig().monitoring_url;
    driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints`, {
      body: monitoringEndpointTemplates,
    });
    return monitoringEndpointTemplates;
  };
