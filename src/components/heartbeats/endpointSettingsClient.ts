import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";

import { EndpointSettings } from "@/resources/EndpointSettings";
import isEndpointSettingsSupported from "@/components/heartbeats/isEndpointSettingsSupported";

class EndpointSettingsClient {
  public async endpointSettings(): Promise<EndpointSettings[]> {
    if (isEndpointSettingsSupported.value) {
      const [, data] = await useTypedFetchFromServiceControl<EndpointSettings[]>(`endpointssettings`);
      return data;
    }

    return [this.defaultEndpointSettingsValue()];
  }

  public defaultEndpointSettingsValue() {
    return <EndpointSettings>{ name: "", track_instances: true };
  }
}

export default new EndpointSettingsClient();
