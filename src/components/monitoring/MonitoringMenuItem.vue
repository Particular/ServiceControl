<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import { stats } from "@/composables/serviceServiceControl";
</script>

<template>
  <RouterLink :to="routeLinks.monitoring.root">
    <i class="fa pa-monitoring icon-white" title="Monitoring"></i>
    <span class="navbar-label">Monitoring</span>
    <span v-if="stats.number_of_disconnected_endpoints > 0" class="badge badge-important">{{ stats.number_of_disconnected_endpoints }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
