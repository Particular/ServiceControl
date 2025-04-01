import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls.ts";
import { EditAndRetryConfig } from "@/resources/Configuration.ts";
import { ref } from "vue";

export const editRetryConfig = ref<EditAndRetryConfig>({ enabled: false, locked_headers: [], sensitive_headers: [] });

async function populate() {
  const [, data] = await useTypedFetchFromServiceControl<EditAndRetryConfig>("edit/config");

  editRetryConfig.value = data;
}
populate();
