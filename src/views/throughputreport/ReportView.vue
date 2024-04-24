<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { Transport } from "./Transport";
import ReportGenerationState from "@/resources/ReportGenerationState";
import throughputClient from "@/views/throughputreport/throughputClient";
import { serviceControlUrl } from "@/composables/serviceServiceControlUrls";

const reportState = ref<ReportGenerationState | null>(null);
const masks = ref<string>("");

onMounted(async () => {
  reportState.value = await throughputClient.reportAvailable();
});

const transport = computed(() => {
  if (reportState.value == null) {
    return Transport.None;
  }

  return reportState.value.transport as Transport;
});

function masksChanged(event: Event) {
  masks.value = (event.target as HTMLInputElement).value;
}

function generateReportUrl() {
  const values = masks.value
    .split("\n")
    .filter((value) => value.length > 0)
    .map((value) => `mask=${encodeURIComponent(value)}`);

  return `${serviceControlUrl.value}throughput/report/file?${values.join("&")}`;
}
</script>

<template>
  <div class="box">
    <div class="row">
      <div class="col-6">
        <label class="form-label">Mask sensitive data</label>
        <textarea class="form-control" rows="3" :value="masks" @input="masksChanged"></textarea>
        <div class="form-text">Masks sensitive information in the generated report. One word per line.</div>
      </div>
      <div class="col-6 text-end">
        <a v-if="reportState?.report_can_be_generated" class="btn btn-primary actions" role="button" :href="generateReportUrl()">Generate Report</a>
        <a v-else class="btn btn-primary disabled actions" aria-disabled="true" role="button">Generate Report</a>
        <p v-if="!reportState?.report_can_be_generated">{{ reportState?.reason }}</p>
      </div>
    </div>
  </div>
  <div class="extra-info" v-if="transport === Transport.MSMQ">
    <p>If you have more endpoints that do not have audit/monitoring turned on, please also send us a screen shot of their MSMQ queues in addition to the report.</p>
  </div>
</template>

<style scoped>
.extra-info {
  margin: 15px 0;
}
</style>
