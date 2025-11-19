import { SetupFactoryOptions } from "../driver";

const content = JSON.stringify(0);

export const hasNoDisconnectedEndpoints = ({ driver }: SetupFactoryOptions) => {
  const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints/disconnected`, {
    body: content,
  });
  return content;
};
