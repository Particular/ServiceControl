<script setup lang="ts">
import { onMounted, ref } from "vue";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { connectionState } from "@/composables/serviceServiceControl";
import ServiceControlNotAvailable from "@/components/ServiceControlNotAvailable.vue";
import ReportGenerationState from "@/resources/ReportGenerationState";
import throughputClient from "@/views/throughputreport/throughputClient";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";

const reportState = ref<ReportGenerationState | null>(null);

onMounted(async () => {
  reportState.value = await throughputClient.reportAvailable();
});

async function generateReport() {
  const spVersion = `spVersion=${encodeURIComponent(window.defaultConfig.version)}`;
  const fileName = await throughputClient.downloadReport(spVersion);

  if (fileName !== "") {
    useShowToast(TYPE.INFO, "Report Generated", `Please email ${fileName} to sales@particular.net`);
  }
}
</script>

<template>
  <ServiceControlNotAvailable />
  <template v-if="connectionState.connected">
    <div class="container">
      <div class="row">
        <div class="col-sm-12">
          <h1>System Throughput</h1>
        </div>
      </div>
      <div class="row">
        <div class="col-sm-12">
          <div class="nav tabs">
            <div>
              <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughputReport.root) }">
                <RouterLink :to="routeLinks.throughputReport.endpoints.link">Endpoints</RouterLink>
              </h5>
              <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughputReport.setup.link) }">
                <RouterLink :to="routeLinks.throughputReport.setup.link">Setup</RouterLink>
              </h5>
              <!--  <h5 class="nav-item" :class="{ active: isRouteSelected(routeLinks.throughputReport.report.link) }">
                <RouterLink :to="routeLinks.throughputReport.report.link">Report</RouterLink>
              </h5> -->
            </div>
            <div class="filter-group">
              <!-- <div>
                <a v-if="reportState?.report_can_be_generated" class="btn btn-primary actions" role="button" :href="generateReportUrl()"><i class="fa fa-download"></i> Generate Report</a>
                <a v-else class="btn btn-primary disabled actions" aria-disabled="true" role="button">Generate Report</a>
              </div> -->
              <div>
                <p v-if="!reportState?.report_can_be_generated">{{ reportState?.reason }}</p>
              </div>
              <button id="generate-report" type="button" class="btn btn-primary actions" @click="generateReport()" :disabled="!reportState?.report_can_be_generated"><i class="fa fa-download"></i> Generate Report</button>
            </div>
          </div>
        </div>
      </div>
      <RouterView />
    </div>
  </template>
</template>

<style scoped></style>
