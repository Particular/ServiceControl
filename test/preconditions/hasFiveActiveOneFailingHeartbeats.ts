import { SetupFactoryOptions } from "../driver";
import { heartbeatsFiveActiveOneFailing } from "../mocks/heartbeat-endpoint-template";
export const hasFiveActiveOneFailingHeartbeats = ({ driver }: SetupFactoryOptions) => {
  const serviceControlInstanceUrl = window.defaultConfig.service_control_url;
  driver.mockEndpoint(`${serviceControlInstanceUrl}heartbeats/stats`, {
    body: heartbeatsFiveActiveOneFailing,
  });
  return heartbeatsFiveActiveOneFailing;
};
