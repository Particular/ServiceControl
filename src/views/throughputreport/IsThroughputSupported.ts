import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export const minimumSCVersionForThroughput = "5.0.0";

export function useIsThroughputSupported() {
  return useIsSupported(environment.sc_version, minimumSCVersionForThroughput);
}
