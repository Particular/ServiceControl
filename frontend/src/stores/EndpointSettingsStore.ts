import { acceptHMRUpdate, defineStore } from "pinia";
import { useServiceControlStore } from "./ServiceControlStore";
import { EndpointSettings } from "@/resources/EndpointSettings";
import useIsEndpointSettingsSupported from "@/components/heartbeats/isEndpointSettingsSupported";

export const useEndpointSettingsStore = defineStore("EndpointSettingsStore", () => {
  const defaultEndpointSettingsValue = <EndpointSettings>{ name: "", track_instances: true };
  const serviceControlStore = useServiceControlStore();

  const isEndpointSettingsSupported = useIsEndpointSettingsSupported();

  async function getEndpointSettings(): Promise<EndpointSettings[]> {
    if (!isEndpointSettingsSupported.value) return [defaultEndpointSettingsValue];

    const [, data] = await serviceControlStore.fetchTypedFromServiceControl<EndpointSettings[]>(`endpointssettings`);
    return data;
  }

  return {
    defaultEndpointSettingsValue,
    getEndpointSettings,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useEndpointSettingsStore, import.meta.hot));
}

export type EndpointSettingsStore = ReturnType<typeof useEndpointSettingsStore>;
