<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <RouterLink :to="routeLinks.throughput.root">
    <i class="fa fa-file-text-o icon-white" title="Usage"></i>
    <span class="navbar-label">Usage</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
