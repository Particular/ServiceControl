import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

const content = JSON.stringify([]);

export const errorsDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = getDefaultConfig().service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}errors`, {
    body: content,
  });
  return content;
};
