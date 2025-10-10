<script setup lang="ts">
import { RouterView } from "vue-router";
import PageFooter from "./components/PageFooter.vue";
import PageHeader from "./components/PageHeader.vue";
import "bootstrap";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { useServiceControlAutoRefresh } from "@/composables/serviceServiceControl";
import LicenseNotifications from "@/components/LicenseNotifications.vue";
import BackendChecksNotifications from "@/components/BackendChecksNotifications.vue";

useServiceControlUrls();
useServiceControlAutoRefresh();
</script>

<template>
  <page-header />
  <div class="container-fluid" id="main-content">
    <RouterView />
  </div>
  <LicenseNotifications />
  <BackendChecksNotifications />
  <page-footer />
</template>
