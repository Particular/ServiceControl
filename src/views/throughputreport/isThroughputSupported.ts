import { computed } from "vue";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export const minimumSCVersionForThroughput = "5.4.0";
const isThroughputSupported = computed(() => useIsSupported(environment.sc_version, minimumSCVersionForThroughput));

export default isThroughputSupported;
