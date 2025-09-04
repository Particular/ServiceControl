<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faFileLines } from "@fortawesome/free-solid-svg-icons";
</script>

<template>
  <RouterLink :to="routeLinks.throughput.root">
    <FAIcon :icon="faFileLines" v-tippy="'Usage'" />
    <span class="navbar-label">Usage</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
