<script setup lang="ts">
import { RouterView } from "vue-router";
import PageFooter from "./components/PageFooter.vue";
import PageHeader from "./components/PageHeader.vue";
import "bootstrap";
import { useLicense } from "@/composables/serviceLicense";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { useServiceControl } from "@/composables/serviceServiceControl";
import "highlight.js/styles/github-dark.css";

// eslint-disable-next-line github/no-then
useServiceControlUrls().then(async () => await Promise.all([useLicense(), useServiceControl()]));
</script>

<template>
  <page-header />
  <div class="container-fluid" id="main-content">
    <RouterView />
  </div>
  <page-footer />
</template>
