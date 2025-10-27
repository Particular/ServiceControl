<script setup lang="ts">
import { ref } from "vue";
import createThroughputClient from "@/views/throughputreport/throughputClient";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import ThroughputSupported from "@/views/throughputreport/ThroughputSupported.vue";
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import FAIcon from "@/components/FAIcon.vue";
import { faDownload } from "@fortawesome/free-solid-svg-icons";
import { computedAsync } from "@vueuse/core";
import useIsThroughputSupported from "./throughputreport/isThroughputSupported";

const showWarning = ref<boolean>(false);

const isThroughputSupported = useIsThroughputSupported();
const throughputClient = createThroughputClient();

const reportState = computedAsync(async () => (isThroughputSupported.value ? await throughputClient.reportAvailable() : null), null);

async function generateReport() {
  const results = await throughputClient.endpoints();
  const hasNonUserIndicatorEndpoint = results.find((value) => !value.user_indicator);

  if (hasNonUserIndicatorEndpoint) {
    showWarning.value = true;
  } else {
    await downloadReport();
  }
}

async function downloadReport() {
  showWarning.value = false;
  const fileName = await throughputClient.downloadReport();

  if (fileName !== "") {
    useShowToast(TYPE.INFO, "Report Downloaded", `Please email '${fileName}' to your account manager.`, true);
  }
}
</script>

<template>
  <ServiceControlAvailable>
    <ThroughputSupported>
      <div class="container">
        <div class="row">
          <div class="col-sm-6">
            <h1>Usage</h1>
          </div>
          <div class="col-sm-6 text-end">
            <span class="reason" v-if="!reportState?.report_can_be_generated">{{ reportState?.reason }}</span>
            <button type="button" aria-label="Download Report" class="btn btn-primary actions" @click="generateReport()" :disabled="!reportState?.report_can_be_generated"><FAIcon :icon="faDownload" /> Download Report</button>
            <Teleport to="#modalDisplay">
              <ConfirmDialog v-if="showWarning" heading="Not all endpoints/queues have an Endpoint Type set" body="Are you sure you want to continue?" @cancel="showWarning = false" @confirm="downloadReport" />
            </Teleport>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12">
            <RouterView />
          </div>
        </div>
      </div>
    </ThroughputSupported>
  </ServiceControlAvailable>
</template>

<style scoped>
.reason {
  margin-left: 5px;
  margin-right: 5px;
}
</style>
