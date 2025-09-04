<script setup lang="ts">
import { RouterLink } from "vue-router";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faEnvelopeOpen } from "@fortawesome/free-solid-svg-icons";
</script>

<template>
  <RouterLink :to="routeLinks.messages.root">
    <FAIcon :icon="faEnvelopeOpen" v-tippy="'All Messages'" />
    <span class="navbar-label">All Messages</span>
  </RouterLink>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
