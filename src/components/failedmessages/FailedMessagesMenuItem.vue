<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import { stats } from "@/composables/serviceServiceControl";
</script>

<template>
  <RouterLink :to="routeLinks.failedMessage.root">
    <i class="fa fa-envelope icon-white" title="Failed Messages"></i>
    <span class="navbar-label">Failed Messages</span>
    <span v-if="stats.number_of_failed_messages > 0" class="badge badge-important">{{ stats.number_of_failed_messages }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
