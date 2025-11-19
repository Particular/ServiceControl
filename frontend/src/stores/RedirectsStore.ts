import Redirect from "@/resources/Redirect";
import QueueAddress from "@/resources/QueueAddress";
import { acceptHMRUpdate, defineStore } from "pinia";
import { reactive } from "vue";
import { useServiceControlStore } from "./ServiceControlStore";

export interface Redirects {
  data: Redirect[];
  queues: string[];
  total: number;
}

export const useRedirectsStore = defineStore("RedirectsStore", () => {
  const redirects = reactive<Redirects>({
    data: [],
    queues: [],
    total: 0,
  });

  const serviceControlStore = useServiceControlStore();

  async function getKnownQueues() {
    const [, data] = await serviceControlStore.fetchTypedFromServiceControl<QueueAddress[]>("errors/queues/addresses");
    redirects.queues = data.map((x) => x.physical_address);
  }

  async function getRedirects() {
    const [response, data] = await serviceControlStore.fetchTypedFromServiceControl<Redirect[]>("redirects");
    redirects.total = parseInt(response.headers.get("Total-Count") || "0");
    redirects.data = data;
  }

  async function refresh() {
    await Promise.all([getRedirects(), getKnownQueues()]);
  }

  async function retryPendingMessagesForQueue(queueName: string) {
    const response = await serviceControlStore.postToServiceControl(`errors/queues/${queueName}/retry`);
    return {
      message: response.ok ? "success" : `error:${response.statusText}`,
      status: response.status,
      statusText: response.statusText,
    };
  }

  return { refresh, redirects, retryPendingMessagesForQueue };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useRedirectsStore, import.meta.hot));
}

export type RedirectsStore = ReturnType<typeof useRedirectsStore>;
