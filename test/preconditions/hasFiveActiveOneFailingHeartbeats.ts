import { SetupFactoryOptions } from "../driver";
import content from "../mocks/heartbeats-five-active-one-failing.json";

export const hasFiveActiveOneFailingHeartbeats = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}heartbeats/stats`, {
    body: content,
  });
  return content;
};
