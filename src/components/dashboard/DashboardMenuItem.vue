<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <RouterLink :to="routeLinks.dashboard">
    <i class="fa fa-dashboard icon-white" title="Dashboard"></i>
    <span class="navbar-label">Dashboard</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
