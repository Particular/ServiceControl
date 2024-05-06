import { useFetchFromServiceControl, usePostToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";
import ConnectionTestResults from "@/resources/ConnectionTestResults";
import ThroughputConnectionSettings from "@/resources/ThroughputConnectionSettings";
import { useDownloadFileFromResponse } from "@/composables/fileDownloadCreator";
import ReportGenerationState from "@/resources/ReportGenerationState";

class ThroughputClient {
  constructor(readonly basePath: string) {}

  public async endpoints() {
    const [_, data] = await useTypedFetchFromServiceControl<EndpointThroughputSummary[]>(`${this.basePath}/endpoints`);

    return data;
  }

  public async updateIndicators(data: UpdateUserIndicator[]): Promise<void> {
    const response = await usePostToServiceControl(`${this.basePath}/endpoints/update`, data);
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

  public async downloadReport(spVersion: string) {
    const response = await useFetchFromServiceControl(`${this.basePath}/report/file?${spVersion}`);
    if (response.ok) {
      var fileName = "throughput-report.json";
      var contentType = "application/json";
      const contentDisposition = response.headers.get("Content-Disposition");
      try {
        if (contentDisposition != null) {
          fileName = contentDisposition.split("filename=")[1].split(";")[0].replaceAll('"', "");
        }
        contentType = response.headers.get("Content-Type")!;
      } catch {
        //do nothing
      }
      await useDownloadFileFromResponse(response, contentType, fileName);
      return fileName;
    }
    return "";
  }

  public async getMasks() {
    //return await useFetchFromServiceControl(`${this.basePath}/settings/masks`);
    const [, data] = await useTypedFetchFromServiceControl<string[]>(`${this.basePath}/settings/masks`);
    return data;
  }

  public async updateMasks(data: string[]): Promise<void> {
    await usePostToServiceControl(`${this.basePath}/settings/masks/update`, data);
  }
}

export default new ThroughputClient("throughput");
