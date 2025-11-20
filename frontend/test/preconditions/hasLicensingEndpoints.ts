import { SetupFactoryOptions } from "../driver";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import { getDefaultConfig } from "@/defaultConfig";

export const hasLicensingEndpoints =
  (
    body: EndpointThroughputSummary[] = [
      <EndpointThroughputSummary>{
        name: "Sender",
        is_known_endpoint: true,
        user_indicator: "",
        max_daily_throughput: 10,
      },
    ]
  ) =>
  ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/endpoints`, {
      body,
      method: "get",
      status: 200,
    });
    return [];
  };
