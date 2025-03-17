<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import { useHeartbeatsStore } from "@/stores/HeartbeatsStore";
import { storeToRefs } from "pinia";

const { failedHeartbeatsCount } = storeToRefs(useHeartbeatsStore());
</script>

<template>
  <RouterLink aria-label="Heartbeats Menu Item" :to="routeLinks.heartbeats.root">
    <i class="fa fa-heartbeat icon-white" title="Heartbeats"></i>
    <span class="navbar-label">Heartbeats</span>
    <span v-if="failedHeartbeatsCount > 0" class="badge badge-important" aria-label="Alert Count">{{ failedHeartbeatsCount }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
