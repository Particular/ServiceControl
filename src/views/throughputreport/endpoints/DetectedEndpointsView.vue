<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
</script>

<template>
  <DetectedListView
    :indicator-options="[UserIndicator.NServiceBusEndpoint, UserIndicator.SendOnlyOrTransactionSessionEndpoint, UserIndicator.NServiceBusEndpointNoLongerInUse, UserIndicator.PlannedToDecommission]"
    :source="DataSource.wellKnownEndpoint"
    column-title="Endpoint Name"
  >
    <template #nodata> No usage data available yet </template>
  </DetectedListView>
</template>

<style scoped></style>
