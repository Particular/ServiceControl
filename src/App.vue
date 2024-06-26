<script setup lang="ts">
import { onMounted } from "vue";
import { RouterView, useRouter } from "vue-router";
import PageFooter from "./components/PageFooter.vue";
import PageHeader from "./components/PageHeader.vue";
import "bootstrap";
import { useLicense } from "@/composables/serviceLicense";
import { useServiceControlUrls } from "@/composables/serviceServiceControlUrls";
import { useServiceControl } from "@/composables/serviceServiceControl";
import "highlight.js/styles/github-dark.css";

useServiceControlUrls();

onMounted(async () => {
  const router = useRouter();
  await Promise.all([useLicense(router), useServiceControl(router)]);
});
</script>

<template>
  <page-header />
  <div class="container-fluid" id="main-content">
    <RouterView />
  </div>
  <page-footer />
</template>
