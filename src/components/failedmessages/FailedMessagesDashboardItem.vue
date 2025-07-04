<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { stats } from "@/composables/serviceServiceControl";
import routeLinks from "@/router/routeLinks";
import { faEnvelope } from "@fortawesome/free-solid-svg-icons";
</script>

<template>
  <DashboardItem :counter="stats.number_of_failed_messages" :url="routeLinks.failedMessage.root" :icon="faEnvelope">Failed Messages</DashboardItem>
</template>
