<script setup lang="ts">
import { RouterLink } from "vue-router";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import routeLinks from "@/router/routeLinks";
import { storeToRefs } from "pinia";
import FAIcon from "@/components/FAIcon.vue";
import { faCheck } from "@fortawesome/free-solid-svg-icons";

const store = useCustomChecksStore();
const { failingCount } = storeToRefs(store);
</script>

<template>
  <RouterLink :to="routeLinks.customChecks">
    <FAIcon :icon="faCheck" v-tippy="'Custom Checks'" />
    <span class="navbar-label">Custom Checks</span>
    <span v-if="failingCount > 0" class="badge badge-important">{{ failingCount }}</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
