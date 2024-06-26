<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import routeLinks from "@/router/routeLinks";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";

const store = useHeartbeatsStore();
</script>

<template>
  <DashboardItem :counter="store.failedHeartbeatsCount" :url="routeLinks.heartbeats.root" :iconClass="'fa-heartbeat'">Heartbeats</DashboardItem>
</template>
