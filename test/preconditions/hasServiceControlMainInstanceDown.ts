import content from "../mocks/service-control-main-instance.json";
import { SetupFactoryOptions } from "../driver";

export const hasServiceControlMainInstanceDown = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(serviceControlInstanceUrl, {
    body: content,
    headers: { "X-Particular-Version": "5.0.4" },
  });
  return content;
};
