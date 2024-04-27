import { SetupFactoryOptions } from "../driver";
import content from "../mocks/heartbeats-four-active-two-failing.json";

export const hasFourActiveTwoFailingHeartbeats = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}heartbeats/stats`, {
    body: content,
  });
  return content;
};
