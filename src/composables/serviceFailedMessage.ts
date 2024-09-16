import { usePatchToServiceControl, usePostToServiceControl } from "./serviceServiceControlUrls";
import type { Ref } from "vue";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export async function useUnarchiveMessage(ids: string[]) {
  const response = await usePatchToServiceControl("errors/unarchive/", ids);
  if (!response.ok) {
    throw new Error(response.statusText);
  }
}

export async function useArchiveMessage(ids: string[]) {
  const response = await usePatchToServiceControl("errors/archive/", ids);
  if (!response.ok) {
    throw new Error(response.statusText);
  }
}

export async function useRetryMessages(ids: string[]) {
  await usePostToServiceControl("errors/retry", ids);
}

export async function useRetryEditedMessage(
  id: string,
  editedMessage: Ref<{
    messageBody: string;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    headers: any[];
  }>
) {
  let headers = editedMessage.value.headers;
  if (useIsSupported(environment.sc_version, "5.2.0")) {
    headers = editedMessage.value.headers.reduce(
      (result, header) => {
        const { key, value } = header as { key: string; value: string };
        result[key] = value;
        return result;
      },
      {} as { [key: string]: string }
    );
  }

  const payload = {
    message_body: editedMessage.value.messageBody,
    message_headers: headers,
  };
  const response = await usePostToServiceControl(`edit/${id}`, payload);
  if (!response.ok) {
    throw new Error(response.statusText);
  }
}
