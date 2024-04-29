import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

const servicePulseFetch = async (input: RequestInfo | URL, version: string, init?: RequestInit) => {
  const requestInit = init ?? {};
  requestInit.headers = new Headers(requestInit.headers);
  if (useIsSupported(version, "5.2.0")) {
    requestInit.headers.set("Particular-ServicePulse-Version", window.defaultConfig.version);
  }

  return await fetch(input, requestInit);
};

export default servicePulseFetch;
