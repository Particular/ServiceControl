<script setup lang="ts">
import { faComment } from "@fortawesome/free-solid-svg-icons";
import FAIcon from "@/components/FAIcon.vue";
</script>

<template>
  <a class="btn-feedback" href="https://github.com/Particular/ServicePulse/issues/new" target="_blank">
    <FAIcon class="icon" :icon="faComment" title="Feedback" />
    <span class="navbar-label">Feedback</span>
  </a>
</template>
<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";

a.btn-feedback,
a.btn-feedback:visited,
a.btn-feedback:focus {
  color: #fff !important;
  background-color: var(--sp-blue) !important;
  padding: 6px 16px !important;
  margin: 14px 0 0 0;
  border-radius: 3px;
}

a.btn-feedback:hover,
a.btn-feedback:focus {
  background-color: hsl(194, 95%, 61%) !important;
}

a.btn-feedback .navbar-label {
  font-weight: bold;
}

.btn-feedback .icon {
  color: #fff;
}
</style>
