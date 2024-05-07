<script setup lang="ts">
import { onMounted, ref } from "vue";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import ReportGenerationState from "@/resources/ReportGenerationState";
import throughputClient from "@/views/throughputreport/throughputClient";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import ServiceControlAvailable from "@/components/ServiceControlAvailable.vue";
import ThroughputSupported from "@/views/throughputreport/ThroughputSupported.vue";

const reportState = ref<ReportGenerationState | null>(null);

onMounted(async () => {
  reportState.value = await throughputClient.reportAvailable();
});

async function generateReport() {
  const fileName = await throughputClient.downloadReport();

  if (fileName !== "") {
    useShowToast(TYPE.INFO, "Report Generated", `Please email ${fileName} to sales@particular.net`);
  }
}
</script>

<template>
  <ServiceControlAvailable>
    <ThroughputSupported>
      <div class="container">
        <div class="row">
          <div class="col-sm-12">
            <h1>System Throughput</h1>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12">
            <div class="nav tabs tabsWithButton">
              <div>
                <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughputReport.root) }">
                  <RouterLink :to="routeLinks.throughputReport.endpoints.link">Endpoints</RouterLink>
                </h5>
                <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughputReport.setup.link) }">
                  <RouterLink :to="routeLinks.throughputReport.setup.link">Setup</RouterLink>
                </h5>
              </div>
              <div class="filter-group">
                <p v-if="!reportState?.report_can_be_generated">{{ reportState?.reason }}</p>
                <button id="generate-report" type="button" class="btn btn-primary actions" @click="generateReport()" :disabled="!reportState?.report_can_be_generated"><i class="fa fa-download"></i> Generate Report</button>
              </div>
            </div>
          </div>
        </div>
        <RouterView />
      </div>
    </ThroughputSupported>
  </ServiceControlAvailable>
</template>

<style scoped>
.tabsWithButton {
  justify-content: space-between;
}
.filter-group {
  display: flex;
  align-items: baseline;
  gap: 1em;
}
</style>
