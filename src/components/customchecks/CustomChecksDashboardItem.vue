<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import routeLinks from "@/router/routeLinks";
import { storeToRefs } from "pinia";
import { useLink } from "vue-router";

const store = useCustomChecksStore();
const { failingCount } = storeToRefs(store);
</script>

<template>
  <DashboardItem :counter="failingCount" :url="useLink({ to: routeLinks.customChecks }).href.value" :iconClass="'fa-check'">Custom Checks</DashboardItem>
</template>
