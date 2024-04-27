<script setup lang="ts">
import DashboardItem from "@/components/DashboardItem.vue";
import { stats } from "@/composables/serviceServiceControl";
import routeLinks from "@/router/routeLinks";
import { useLink } from "vue-router";
</script>

<template>
  <DashboardItem :counter="stats.number_of_failed_messages" :url="useLink({ to: routeLinks.failedMessage.root }).href.value" :iconClass="'fa-envelope'">Failed Messages</DashboardItem>
</template>
