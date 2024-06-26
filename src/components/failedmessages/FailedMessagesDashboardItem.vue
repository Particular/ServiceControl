<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { stats } from "@/composables/serviceServiceControl";
import routeLinks from "@/router/routeLinks";
</script>

<template>
  <DashboardItem :counter="stats.number_of_failed_messages" :url="routeLinks.failedMessage.root" :iconClass="'fa-envelope'">Failed Messages</DashboardItem>
</template>
