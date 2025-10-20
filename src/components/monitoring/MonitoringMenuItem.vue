<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faChartLine } from "@fortawesome/free-solid-svg-icons";
import { storeToRefs } from "pinia";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const { store: statsStore } = useConnectionsAndStatsAutoRefresh();
const { disconnectedEndpointsCount } = storeToRefs(statsStore);
</script>

<template>
  <RouterLink :to="routeLinks.monitoring.root">
    <FAIcon :icon="faChartLine" title="Monitoring" />
    <span class="navbar-label">Monitoring</span>
    <span v-if="disconnectedEndpointsCount > 0" class="badge badge-important">{{ disconnectedEndpointsCount }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
