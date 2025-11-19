import { eventLogItems } from "../mocks/event-log-items-template";
import { SetupFactoryOptions } from "../driver";

export const hasEventLogItems = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}eventlogitems`, {
    body: eventLogItems,
  });
  return eventLogItems;
};
