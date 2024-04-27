import content from "../mocks/event-log-items.json";
import { SetupFactoryOptions } from "../driver";

export const hasEventLogItems = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}eventlogitems`, {
    body: content,
  });
  return content;
};
