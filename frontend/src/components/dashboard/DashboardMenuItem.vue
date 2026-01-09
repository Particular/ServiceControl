<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import { faGauge } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
</script>

<template>
  <RouterLink :to="routeLinks.dashboard">
    <FAIcon :icon="faGauge" title="Dashboard" />
    <span class="navbar-label">Dashboard</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
