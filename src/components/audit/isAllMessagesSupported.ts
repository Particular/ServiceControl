import { computed } from "vue";
import { useIsSupported } from "@/composables/serviceSemVer";
import { environment } from "@/composables/serviceServiceControl";

export const minimumSCVersionForAllMessages = "6.6.0";
const isAllMessagesSupported = computed(() => useIsSupported(environment.sc_version, minimumSCVersionForAllMessages));

export default isAllMessagesSupported;
