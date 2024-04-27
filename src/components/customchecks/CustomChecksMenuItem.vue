<script setup lang="ts">
import { RouterLink } from "vue-router";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import routeLinks from "@/router/routeLinks";
import { storeToRefs } from "pinia";

const store = useCustomChecksStore();
const { failingCount } = storeToRefs(store);
</script>

<template>
  <RouterLink :to="routeLinks.customChecks">
    <i class="fa fa-check icon-white" title="Custom Checks"></i>
    <span class="navbar-label">Custom Checks</span>
    <span v-if="failingCount > 0" class="badge badge-important">{{ failingCount }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
