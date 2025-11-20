import { eventLogItems } from "../mocks/event-log-items-template";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasEventLogItems = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}eventlogitems`, {
    body: eventLogItems,
  });
  return eventLogItems;
};
