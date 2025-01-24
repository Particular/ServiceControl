import Configuration from "@/resources/Configuration";
import { ref } from "vue";
import { useFetchFromServiceControl } from "./serviceServiceControlUrls";

export function useConfiguration() {
  const configuration = ref<Configuration | null>(null);

  async function populate() {
    const response = await useFetchFromServiceControl("configuration");
    configuration.value = await response.json();
  }
  populate();

  return configuration;
}
