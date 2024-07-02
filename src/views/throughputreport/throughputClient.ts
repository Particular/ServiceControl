import { useFetchFromServiceControl, usePostToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { useDownloadFileFromResponse } from "@/composables/fileDownloadCreator";
import ReportGenerationState from "@/resources/ReportGenerationState";
import { parse } from "@tinyhttp/content-disposition";
import isThroughputSupported from "@/views/throughputreport/isThroughputSupported";

class ThroughputClient {
  constructor(readonly basePath: string) {}

  public async endpoints() {
    const [_, data] = await useTypedFetchFromServiceControl<EndpointThroughputSummary[]>(`${this.basePath}/endpoints`);

    return data;
  }

  public async updateIndicators(data: UpdateUserIndicator[]): Promise<void> {
    await usePostToServiceControl(`${this.basePath}/endpoints/update`, data);
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
    if (isThroughputSupported.value) {
      const [, data] = await useTypedFetchFromServiceControl<ReportGenerationState>(`${this.basePath}/report/available`);
      return data;
    }
    return null;    
  }

  public async downloadReport() {
    const response = await useFetchFromServiceControl(`${this.basePath}/report/file?spVersion=${encodeURIComponent(window.defaultConfig.version)}`);
    if (response.ok) {
      let fileName = "throughput-report.json";
      const contentType = response.headers.get("Content-Type") ?? "application/json";
      const contentDisposition = response.headers.get("Content-Disposition");
      try {
        if (contentDisposition != null) {
          parse(contentDisposition);
          fileName = parse(contentDisposition).parameters["filename"] as string;
        }
      } catch {
        //do nothing
      }
      await useDownloadFileFromResponse(response, contentType, fileName);
      return fileName;
    }
    return "";
  }

  public async getMasks() {
    if (isThroughputSupported.value) {
    const [, data] = await useTypedFetchFromServiceControl<string[]>(`${this.basePath}/settings/masks`);
    return data;
    }
    return [];
  }

  public async updateMasks(data: string[]): Promise<void> {
    await usePostToServiceControl(`${this.basePath}/settings/masks/update`, data);
  }
}

export default new ThroughputClient("licensing");
