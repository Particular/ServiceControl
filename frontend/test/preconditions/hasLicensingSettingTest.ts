import ConnectionTestResults, { ConnectionSettingsTestResult } from "@/resources/ConnectionTestResults";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasLicensingSettingTest =
  (body: Partial<ConnectionTestResults> = {}) =>
  ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/settings/test`, {
      body: {
        ...(<ConnectionTestResults>{
          transport: "",
          audit_connection_result: <ConnectionSettingsTestResult>{
            connection_successful: true,
            connection_error_messages: [],
            diagnostics: "",
          },
          monitoring_connection_result: <ConnectionSettingsTestResult>{
            connection_successful: true,
            connection_error_messages: [],
            diagnostics: "",
          },
          broker_connection_result: <ConnectionSettingsTestResult>{
            connection_successful: true,
            connection_error_messages: [],
            diagnostics: "",
          },
        }),
        ...body,
      },
      method: "get",
      status: 200,
    });
  };
