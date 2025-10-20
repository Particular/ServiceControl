import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { EndpointSettings } from "@/resources/EndpointSettings";

export async function getEndpointSettings(): Promise<EndpointSettings[]> {
  const [, data] = await useTypedFetchFromServiceControl<EndpointSettings[]>(`endpointssettings`);
  return data;
}

export function defaultEndpointSettingsValue() {
  return <EndpointSettings>{ name: "", track_instances: true };
}
