<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { useCustomChecksStore } from "@/stores/CustomChecksStore";
import routeLinks from "@/router/routeLinks";
import { storeToRefs } from "pinia";
import { faCheck } from "@fortawesome/free-solid-svg-icons";

const store = useCustomChecksStore();
const { failingCount } = storeToRefs(store);
</script>

<template>
  <DashboardItem :counter="failingCount" :url="routeLinks.customChecks" :icon="faCheck">Custom Checks</DashboardItem>
</template>
