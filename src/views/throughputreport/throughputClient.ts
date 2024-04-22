import { useFetchFromServiceControl, usePostToServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import EndpointThroughputSummary from "@/resources/EndpointThroughputSummary";
import UpdateUserIndicator from "@/resources/UpdateUserIndicator";

class ThroughputClient {
  constructor(readonly basePath: string) {}

  public async endpoints(): Promise<EndpointThroughputSummary[]> {
    const [_, data] = await useTypedFetchFromServiceControl<EndpointThroughputSummary[]>(`${this.basePath}/endpoints`);

    return data;
  }

  public async updateIndicators(data: UpdateUserIndicator[]): Promise<void> {
    const response = await usePostToServiceControl(`${this.basePath}/endpoints/update`, data);
  }
}

export default new ThroughputClient("throughput");
