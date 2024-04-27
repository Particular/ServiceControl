<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import routeLinks from "@/router/routeLinks";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { useLink } from "vue-router";

const store = useHeartbeatsStore();
</script>

<template>
  <DashboardItem :counter="store.failedHeartbeatsCount" :url="useLink({ to: routeLinks.heartbeats.root }).href.value" :iconClass="'fa-heartbeat'">Heartbeats</DashboardItem>
</template>
