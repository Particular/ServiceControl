import { useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls.ts";
import { EditAndRetryConfig } from "@/resources/Configuration.ts";
import { ref } from "vue";

const editAndRetry = ref<EditAndRetryConfig>();

async function populate() {
  const [, data] = await useTypedFetchFromServiceControl<EditAndRetryConfig>("edit/config");

  editAndRetry.value = data;
}
populate();

export default function useEditAndRetry(): EditAndRetryConfig {
  return editAndRetry.value ?? { enabled: false, locked_headers: [], sensitive_headers: [] };
}
