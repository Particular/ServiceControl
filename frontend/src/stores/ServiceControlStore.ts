import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";
import { getDefaultConfig } from "@/defaultConfig";

export const useServiceControlStore = defineStore("ServiceControlStore", () => {
  const serviceControlUrl = ref<string | null>();
  const monitoringUrl = ref<string | null>();

  const isMonitoringDisabled = computed(() => monitoringUrl.value == null || monitoringUrl.value === "" || monitoringUrl.value === "!");
  const isMonitoringEnabled = computed(() => !isMonitoringDisabled.value);

  function getServiceControlUrl() {
    if (!serviceControlUrl.value) {
      refresh();
    }
    if (!serviceControlUrl.value) {
      throw new Error("Service Control URL is not configured");
    }
    return serviceControlUrl.value;
  }

  function getMonitoringUrl() {
    if (!monitoringUrl.value) refresh();
    return monitoringUrl.value;
  }

  function refresh() {
    const params = new URLSearchParams(window.location.search);
    const mu = params.get("mu");
    const config = getDefaultConfig();

    if (config.service_control_url) {
      serviceControlUrl.value = config.service_control_url;
      console.debug(`setting ServiceControl Url to its default value: ${config.service_control_url}`);
    } else {
      console.warn("ServiceControl Url is not defined.");
    }

    if (mu) {
      monitoringUrl.value = mu;
      window.localStorage.setItem("mu", monitoringUrl.value);
      console.debug(`Monitoring Url found in QS and stored in local storage: ${monitoringUrl.value}`);
    } else if (window.localStorage.getItem("mu")) {
      monitoringUrl.value = window.localStorage.getItem("mu");
      console.debug(`Monitoring Url, not in QS, found in local storage: ${monitoringUrl.value}`);
    } else if (config.monitoring_url) {
      monitoringUrl.value = config.monitoring_url;
      console.debug(`setting Monitoring Url to its default value: ${config.monitoring_url}`);
    } else {
      console.warn("Monitoring Url is not defined.");
    }
  }

  async function fetchFromServiceControl(suffix: string, options?: { cache?: RequestCache }) {
    const requestOptions: RequestInit = {
      method: "GET",
      cache: options?.cache ?? "default", // Default  if not specified
      headers: {
        Accept: "application/json",
      },
    };
    return await fetch(`${getServiceControlUrl()}${suffix}`, requestOptions);
  }

  async function fetchTypedFromServiceControl<T>(suffix: string): Promise<[Response, T]> {
    const response = await fetch(`${getServiceControlUrl()}${suffix}`);
    if (!response.ok) throw new Error(response.statusText ?? "No response");
    const data = await response.json();

    return [response, data];
  }

  async function fetchTypedFromMonitoring<T>(suffix: string): Promise<[Response?, T?]> {
    if (isMonitoringDisabled.value) {
      return [];
    }

    const response = await fetch(`${getMonitoringUrl()}${suffix}`);
    const data = await response.json();

    return [response, data];
  }

  async function postToServiceControl(suffix: string, payload: object | null = null) {
    const requestOptions: RequestInit = {
      method: "POST",
    };
    if (payload != null) {
      requestOptions.headers = { "Content-Type": "application/json" };
      requestOptions.body = JSON.stringify(payload);
    }
    return await fetch(`${getServiceControlUrl()}${suffix}`, requestOptions);
  }

  async function putToServiceControl(suffix: string, payload: object | null) {
    const requestOptions: RequestInit = {
      method: "PUT",
    };
    if (payload != null) {
      requestOptions.headers = { "Content-Type": "application/json" };
      requestOptions.body = JSON.stringify(payload);
    }
    return await fetch(`${getServiceControlUrl()}${suffix}`, requestOptions);
  }

  async function deleteFromServiceControl(suffix: string) {
    const requestOptions: RequestInit = {
      method: "DELETE",
    };
    return await fetch(`${getServiceControlUrl()}${suffix}`, requestOptions);
  }

  async function deleteFromMonitoring(suffix: string) {
    const requestOptions = {
      method: "DELETE",
    };
    return await fetch(`${getMonitoringUrl()}${suffix}`, requestOptions);
  }

  async function optionsFromMonitoring() {
    if (isMonitoringDisabled.value) {
      return Promise.resolve(null);
    }

    const requestOptions = {
      method: "OPTIONS",
    };
    return await fetch(getMonitoringUrl() ?? "", requestOptions);
  }

  async function patchToServiceControl(suffix: string, payload: object | null) {
    const requestOptions: RequestInit = {
      method: "PATCH",
    };
    if (payload != null) {
      requestOptions.headers = { "Content-Type": "application/json" };
      requestOptions.body = JSON.stringify(payload);
    }
    return await fetch(`${getServiceControlUrl()}${suffix}`, requestOptions);
  }

  return {
    refresh,
    serviceControlUrl,
    monitoringUrl,
    isMonitoringDisabled,
    isMonitoringEnabled,
    fetchFromServiceControl,
    fetchTypedFromServiceControl,
    fetchTypedFromMonitoring,
    putToServiceControl,
    postToServiceControl,
    patchToServiceControl,
    deleteFromServiceControl,
    deleteFromMonitoring,
    optionsFromMonitoring,
  };
});

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useServiceControlStore, import.meta.hot));
}

export type ServiceControlStore = ReturnType<typeof useServiceControlStore>;
