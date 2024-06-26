<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import routeLinks from "@/router/routeLinks";
import { storeToRefs } from "pinia";

const store = useCustomChecksStore();
const { failingCount } = storeToRefs(store);
</script>

<template>
  <DashboardItem :counter="failingCount" :url="routeLinks.customChecks" :iconClass="'fa-check'">Custom Checks</DashboardItem>
</template>
