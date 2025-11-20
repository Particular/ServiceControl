import ReportGenerationState from "@/resources/ReportGenerationState";
import { SetupFactoryOptions } from "../driver";
import { getDefaultConfig } from "@/defaultConfig";

export const hasLicensingReportAvailable =
  (body: Partial<ReportGenerationState> = {}) =>
  ({ driver }: SetupFactoryOptions) => {
    driver.mockEndpoint(`${getDefaultConfig().service_control_url}licensing/report/available`, {
      body: {
        ...(<ReportGenerationState>{
          transport: "LearningTransport",
          report_can_be_generated: true,
          reason: "",
        }),
        ...body,
      },
      method: "get",
      status: 200,
    });
  };
