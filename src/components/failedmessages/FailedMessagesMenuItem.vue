<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faEnvelope } from "@fortawesome/free-solid-svg-icons";
import { storeToRefs } from "pinia";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const { store } = useConnectionsAndStatsAutoRefresh();
const { failedMessageCount } = storeToRefs(store);
</script>

<template>
  <RouterLink :to="routeLinks.failedMessage.root">
    <FAIcon :icon="faEnvelope" title="Failed Messages" />
    <span class="navbar-label">Failed Messages</span>
    <span v-if="failedMessageCount > 0" class="badge badge-important">{{ failedMessageCount }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
