import { useFetchFromServiceControl, postToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { useDownloadFileFromResponse } from "@/composables/fileDownloadCreator";
import ReportGenerationState from "@/resources/ReportGenerationState";
import { parse } from "@tinyhttp/content-disposition";

class ThroughputClient {
  constructor(readonly basePath: string) {}

  public async endpoints() {
    const [, data] = await useTypedFetchFromServiceControl<EndpointThroughputSummary[]>(`${this.basePath}/endpoints`);

    return data;
  }

  public async updateIndicators(data: UpdateUserIndicator[]): Promise<void> {
    await postToServiceControl(`${this.basePath}/endpoints/update`, data);
  }

  public async test() {
    const [, data] = await useTypedFetchFromServiceControl<ConnectionTestResults>(`${this.basePath}/settings/test`);
    return data;
  }

  public async setting() {
    const [, data] = await useTypedFetchFromServiceControl<ThroughputConnectionSettings>(`${this.basePath}/settings/info`);
    return data;
  }

  public async reportAvailable() {
    const [, data] = await useTypedFetchFromServiceControl<ReportGenerationState>(`${this.basePath}/report/available`);
    return data;
  }

  public async downloadReport() {
    const response = await useFetchFromServiceControl(`${this.basePath}/report/file?spVersion=${encodeURIComponent(window.defaultConfig.version)}`);
    if (response.ok) {
      let fileName = "throughput-report.json";
      const contentType = response.headers.get("Content-Type") ?? "application/json";
      const contentDisposition = response.headers.get("Content-Disposition");
      try {
        if (contentDisposition != null) {
          fileName = parse(contentDisposition).parameters["filename"] as string;
        }
      } catch {
        //fallback to the default name, if filename is missing in response header
      }
      await useDownloadFileFromResponse(response, contentType, fileName);
      return fileName;
    }
    return "";
  }

  public async getMasks() {
    const [, data] = await useTypedFetchFromServiceControl<string[]>(`${this.basePath}/settings/masks`);
    return data;
  }

  public async updateMasks(data: string[]): Promise<void> {
    await postToServiceControl(`${this.basePath}/settings/masks/update`, data);
  }
}

export default new ThroughputClient("licensing");
