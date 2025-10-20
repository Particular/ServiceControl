<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";
import routeLinks from "@/router/routeLinks";
import { faEnvelope } from "@fortawesome/free-solid-svg-icons";
import { storeToRefs } from "pinia";

const { store: statsStore } = useConnectionsAndStatsAutoRefresh();
const { failedMessageCount } = storeToRefs(statsStore);
</script>

<template>
  <DashboardItem :counter="failedMessageCount" :url="routeLinks.failedMessage.root" :icon="faEnvelope">Failed Messages</DashboardItem>
</template>
