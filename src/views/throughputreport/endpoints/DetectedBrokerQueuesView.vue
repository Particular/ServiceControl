<script setup lang="ts">
import DetectedListView from "@/views/throughputreport/endpoints/DetectedListView.vue";
import { DataSource } from "@/views/throughputreport/endpoints/dataSource";
import { UserIndicator } from "@/views/throughputreport/endpoints/userIndicator";
</script>

<template>
  <DetectedListView
    :indicator-options="[UserIndicator.NServiceBusEndpoint, UserIndicator.NotNServiceBusEndpoint, UserIndicator.SendOnlyOrTransactionSessionEndpoint, UserIndicator.NServiceBusEndpointNoLongerInUse, UserIndicator.PlannedToDecommission]"
    :source="DataSource.broker"
    column-title="Queue Name"
  />
</template>

<style scoped></style>
