import content from "../mocks/historyPeriodForOneMinute.json";
import { SetupFactoryOptions } from "../driver";

const monitoringInstanceUrl = window.defaultConfig.monitoring_urls[0];

export const hasHistoryPeriodDataForOneMinute = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`${monitoringInstanceUrl}monitored-endpoints?history=1`, {
    body: content,
  });
  return content;
};
