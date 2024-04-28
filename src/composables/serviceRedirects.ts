import { useDeleteFromServiceControl, usePostToServiceControl, usePutToServiceControl, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type Redirect from "@/resources/Redirect";
import type QueueAddress from "@/resources/QueueAddress";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export interface Redirects {
  data: Redirect[];
  queues: string[];
  total: number;
}

export async function useRedirects() {
  const redirects: Redirects = {
    data: [],
    queues: [],
    total: 0,
  };

  async function getKnownQueues() {
    const [, data] = await useTypedFetchFromServiceControl<QueueAddress[]>("errors/queues/addresses");
    redirects.queues = data.map((x) => x.physical_address);
  }

  async function getRedirects() {
    const [response, data] = await useTypedFetchFromServiceControl<Redirect[]>("redirects");
    redirects.total = parseInt(response.headers.get("Total-Count") || "0");
    redirects.data = data;
  }

  await Promise.all([getRedirects(), getKnownQueues()]);

  return redirects;
}

export async function useRetryPendingMessagesForQueue(queueName: string) {
  const response = await usePostToServiceControl(`errors/queues/${queueName}/retry`);
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: response.statusText,
  };
}

export async function useUpdateRedirects(redirectId: string, sourceEndpoint: string, targetEndpoint: string) {
  const response = await usePutToServiceControl(`redirects/${redirectId}`, {
    id: redirectId,
    fromphysicaladdress: sourceEndpoint,
    tophysicaladdress: targetEndpoint,
  });

  const responseStatusText = useIsSupported(environment.sc_version, "5.2.0") ? response.headers.get("X-Particular-Reason") : response.statusText;
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: responseStatusText,
    data: response,
  };
}

export async function useCreateRedirects(sourceEndpoint: string, targetEndpoint: string) {
  const response = await usePostToServiceControl("redirects", {
    fromphysicaladdress: sourceEndpoint,
    tophysicaladdress: targetEndpoint,
  });

  const responseStatusText = useIsSupported(environment.sc_version, "5.2.0") ? response.headers.get("X-Particular-Reason") : response.statusText;
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: responseStatusText,
  };
}

export async function useDeleteRedirects(redirectId: string) {
  const response = await useDeleteFromServiceControl(`redirects/${redirectId}`);
  const responseStatusText = useIsSupported(environment.sc_version, "5.2.0") ? response.headers.get("X-Particular-Reason") : response.statusText;
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: responseStatusText,
    data: response,
  };
}
