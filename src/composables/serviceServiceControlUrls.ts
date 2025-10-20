import { type Ref, ref } from "vue";

const serviceControlUrl = ref<string | null>();
const monitoringUrl = ref<string | null>();

function useServiceControlUrls() {
  const params = getParams();
  const scu = getParameter(params, "scu");
  const mu = getParameter(params, "mu");

  if (scu) {
    serviceControlUrl.value = scu.value;
    window.localStorage.setItem("scu", serviceControlUrl.value);
    console.debug(`ServiceControl Url found in QS and stored in local storage: ${serviceControlUrl.value}`);
  } else if (window.localStorage.getItem("scu")) {
    serviceControlUrl.value = window.localStorage.getItem("scu");
    console.debug(`ServiceControl Url, not in QS, found in local storage: ${serviceControlUrl.value}`);
  } else if (window.defaultConfig && window.defaultConfig.service_control_url) {
    serviceControlUrl.value = window.defaultConfig.service_control_url;
    console.debug(`setting ServiceControl Url to its default value: ${window.defaultConfig.service_control_url}`);
  } else {
    console.warn("ServiceControl Url is not defined.");
  }

  if (mu) {
    monitoringUrl.value = mu.value;
    window.localStorage.setItem("mu", monitoringUrl.value);
    console.debug(`Monitoring Url found in QS and stored in local storage: ${monitoringUrl.value}`);
  } else if (window.localStorage.getItem("mu")) {
    monitoringUrl.value = window.localStorage.getItem("mu");
    console.debug(`Monitoring Url, not in QS, found in local storage: ${monitoringUrl.value}`);
  } else if (window.defaultConfig && window.defaultConfig.monitoring_urls && window.defaultConfig.monitoring_urls.length) {
    monitoringUrl.value = window.defaultConfig.monitoring_urls[0];
    console.debug(`setting Monitoring Url to its default value: ${window.defaultConfig.monitoring_urls[0]}`);
  } else {
    console.warn("Monitoring Url is not defined.");
  }
}

export { useServiceControlUrls, serviceControlUrl, monitoringUrl };

//TODO: the callsites of this should be relying on a computed boolean, rather than a boolean, so that when it changes they also change
export function isMonitoringDisabled() {
  return monitoringUrl.value == null || monitoringUrl.value === "" || monitoringUrl.value === "!";
}

export function isMonitoringEnabled() {
  return !isMonitoringDisabled();
}

export function useFetchFromServiceControl(suffix: string, options?: { cache?: RequestCache }) {
  const requestOptions: RequestInit = {
    method: "GET",
    cache: options?.cache ?? "default", // Default  if not specified
    headers: {
      Accept: "application/json",
    },
  };
  return fetch(serviceControlUrl.value + suffix, requestOptions);
}

export async function useTypedFetchFromServiceControl<T>(suffix: string): Promise<[Response, T]> {
  const response = await fetch(`${serviceControlUrl.value}${suffix}`);
  if (!response.ok) throw new Error(response.statusText ?? "No response");
  const data = await response.json();

  return [response, data];
}

export async function useTypedFetchFromMonitoring<T>(suffix: string): Promise<[Response?, T?]> {
  if (isMonitoringDisabled()) {
    return [];
  }

  const response = await fetch(`${monitoringUrl.value}${suffix}`);
  const data = await response.json();

  return [response, data];
}

export function postToServiceControl(suffix: string, payload: object | null = null) {
  const requestOptions: RequestInit = {
    method: "POST",
  };
  if (payload != null) {
    requestOptions.headers = { "Content-Type": "application/json" };
    requestOptions.body = JSON.stringify(payload);
  }
  return fetch(serviceControlUrl.value + suffix, requestOptions);
}

export function putToServiceControl(suffix: string, payload: object | null) {
  const requestOptions: RequestInit = {
    method: "PUT",
  };
  if (payload != null) {
    requestOptions.headers = { "Content-Type": "application/json" };
    requestOptions.body = JSON.stringify(payload);
  }
  return fetch(serviceControlUrl.value + suffix, requestOptions);
}

export function deleteFromServiceControl(suffix: string) {
  const requestOptions: RequestInit = {
    method: "DELETE",
  };
  return fetch(serviceControlUrl.value + suffix, requestOptions);
}
export function useDeleteFromMonitoring(suffix: string) {
  const requestOptions = {
    method: "DELETE",
  };
  return fetch(monitoringUrl.value + suffix, requestOptions);
}

export function useOptionsFromMonitoring() {
  if (isMonitoringDisabled()) {
    return Promise.resolve(null);
  }

  const requestOptions = {
    method: "OPTIONS",
  };
  return fetch(monitoringUrl.value ?? "", requestOptions);
}

export function patchToServiceControl(suffix: string, payload: object | null) {
  const requestOptions: RequestInit = {
    method: "PATCH",
  };
  if (payload != null) {
    requestOptions.headers = { "Content-Type": "application/json" };
    requestOptions.body = JSON.stringify(payload);
  }
  return fetch(serviceControlUrl.value + suffix, requestOptions);
}

export function updateServiceControlUrls(newServiceControlUrl: Ref<string | null | undefined>, newMonitoringUrl: Ref<string | null | undefined>) {
  if (!newServiceControlUrl.value) {
    throw new Error("ServiceControl URL is mandatory");
  } else if (!newServiceControlUrl.value.endsWith("/")) {
    newServiceControlUrl.value += "/";
  }

  if (!newMonitoringUrl.value) {
    newMonitoringUrl.value = "!"; //disabled
  } else if (!newMonitoringUrl.value.endsWith("/") && newMonitoringUrl.value !== "!") {
    newMonitoringUrl.value += "/";
  }

  //values have changed. They'll be reset after page reloads
  window.localStorage.removeItem("scu");
  window.localStorage.removeItem("mu");

  const newSearch = `?scu=${newServiceControlUrl.value}&mu=${newMonitoringUrl.value}`;
  console.debug("updateConnections - new query string: ", newSearch);
  window.location.search = newSearch;
}

interface Param {
  name: string;
  value: string;
}

function getParams() {
  const params: Param[] = [];

  if (!window.location.search) return params;

  const searchParams = window.location.search.split("&");

  searchParams.forEach((p) => {
    p = p.startsWith("?") ? p.substring(1, p.length) : p;
    const singleParam = p.split("=");
    params.push({ name: singleParam[0], value: singleParam[1] });
  });
  return params;
}

function getParameter(params: Param[], key: string) {
  return params.find((param) => {
    return param.name === key;
  });
}
