import QueueAddress from "@/resources/QueueAddress";
import Redirect from "@/resources/Redirect";
import { SetupFactoryOptions } from "test/driver";

export const knownQueuesDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`${window.defaultConfig.service_control_url}errors/queues/addresses`, {
    body: <QueueAddress[]>[],
  });
};

export const redirectsDefaultHandler = ({ driver }: SetupFactoryOptions) => {
  driver.mockEndpoint(`${window.defaultConfig.service_control_url}redirects`, {
    body: <Redirect[]>[],
  });
};
