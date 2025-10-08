<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import useHeartbeatsStoreAutoRefresh from "@/composables/useHeartbeatsStoreAutoRefresh";
import routeLinks from "@/router/routeLinks";
import { faHeartbeat } from "@fortawesome/free-solid-svg-icons";

const { store } = useHeartbeatsStoreAutoRefresh();
</script>

<template>
  <DashboardItem :counter="store.failedHeartbeatsCount" :url="routeLinks.heartbeats.root" :icon="faHeartbeat">Heartbeats</DashboardItem>
</template>
