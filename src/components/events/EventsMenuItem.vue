<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <RouterLink :to="routeLinks.events">
    <i class="fa fa-list-ul icon-white" title="Events"></i>
    <span class="navbar-label">Events</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
