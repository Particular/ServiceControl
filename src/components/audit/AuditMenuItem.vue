<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <RouterLink :to="routeLinks.messages.root">
    <i class="fa fa-envelope icon-white" title="All Messages"></i>
    <span class="navbar-label">All Messages</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
