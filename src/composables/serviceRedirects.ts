import { postToServiceControl, useTypedFetchFromServiceControl } from "./serviceServiceControlUrls";
import type Redirect from "@/resources/Redirect";
import type QueueAddress from "@/resources/QueueAddress";

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
  const response = await postToServiceControl(`errors/queues/${queueName}/retry`);
  return {
    message: response.ok ? "success" : `error:${response.statusText}`,
    status: response.status,
    statusText: response.statusText,
  };
}
