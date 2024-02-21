<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <div class="container">
    <div class="row">
      <div class="col-sm-12">
        <h1>Throughput Report</h1>
      </div>
    </div>
    <div class="row">
      <div class="col-sm-12">
        <div class="nav tabs">
          <h5 class="nav-item">
            <RouterLink :to="routeLinks.throughputReport.endpoints.link">Endpoints</RouterLink>
          </h5>
          <h5 class="nav-item">
            <RouterLink :to="routeLinks.throughputReport.setup.link">Setup</RouterLink>
          </h5>
          <h5 class="nav-item">
            <RouterLink :to="routeLinks.throughputReport.report.link">Report</RouterLink>
          </h5>
        </div>
      </div>
    </div>
    <RouterView />
  </div>
</template>

<style scoped></style>
