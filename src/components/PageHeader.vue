<script setup lang="ts">
import { computed } from "vue";
import { RouterLink } from "vue-router";
import { useIsMonitoringEnabled } from "@/composables/serviceServiceControlUrls";
import routeLinks from "@/router/routeLinks";
import CustomChecksMenuItem from "@/components/customchecks/CustomChecksMenuItem.vue";
import HeartbeatsMenuItem from "@/components/heartbeats/HeartbeatsMenuItem.vue";
import ConfigurationMenuItem from "@/components/configuration/ConfigurationMenuItem.vue";
import FailedMessagesMenuItem from "@/components/failedmessages/FailedMessagesMenuItem.vue";
import MonitoringMenuItem from "@/components/monitoring/MonitoringMenuItem.vue";
import EventsMenuItem from "@/components/events/EventsMenuItem.vue";
import DashboardMenuItem from "@/components/dashboard/DashboardMenuItem.vue";
import FeedbackButton from "@/components/FeedbackButton.vue";
import ThroughputMenuItem from "@/views/throughputreport/ThroughputMenuItem.vue";
import AuditMenuItem from "./audit/AuditMenuItem.vue";

// prettier-ignore
const menuItems = computed(
  () => [
  DashboardMenuItem,
  HeartbeatsMenuItem,
  ...(useIsMonitoringEnabled() ? [MonitoringMenuItem] : []),
  ...(window.defaultConfig.showAllMessages ? [AuditMenuItem] : []),
  FailedMessagesMenuItem,
  CustomChecksMenuItem,
  EventsMenuItem,
  ThroughputMenuItem,
  ConfigurationMenuItem,
  FeedbackButton,
]);
</script>

<template>
  <nav class="navbar navbar-expand-lg navbar-inverse navbar-dark">
    <div class="container-fluid">
      <div class="navbar-header">
        <RouterLink class="navbar-brand" :to="routeLinks.dashboard">
          <img alt="Service Pulse" src="@/assets/logo.svg" />
        </RouterLink>
      </div>

      <div id="navbar" class="navbar navbar-expand-lg">
        <ul class="nav navbar-nav navbar-inverse">
          <li v-for="menuItem in menuItems" :key="menuItem?.name">
            <component :is="menuItem" />
          </li>
        </ul>
      </div>
    </div>
  </nav>
</template>

<style scoped>
@import "@/assets/navbar.css";
@import "@/assets/header-menu-item.css";
</style>
