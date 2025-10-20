<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
import { licenseStatus } from "@/composables/serviceLicense";
import LicenseExpired from "../components/LicenseExpired.vue";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";
import { storeToRefs } from "pinia";
import useConnectionsAndStatsAutoRefresh from "@/composables/useConnectionsAndStatsAutoRefresh";

const showPendingRetry = window.defaultConfig.showPendingRetry;
const { store: connectionsAndStatsStore } = useConnectionsAndStatsAutoRefresh();
connectionsAndStatsStore.requiresFullFailureDetails();
const connectionState = connectionsAndStatsStore.connectionState;
const { failedMessageCount, archivedMessageCount, pendingRetriesMessageCount } = storeToRefs(connectionsAndStatsStore);
</script>

<template>
  <LicenseExpired />
  <template v-if="!licenseStatus.isExpired">
    <div class="container">
      <div class="row">
        <div class="col-12">
          <h1>Failed Messages</h1>
        </div>
      </div>
      <div class="row">
        <div class="col-sm-12">
          <div class="tabs">
            <!--Failed Message Groups-->
            <h5 :class="{ active: isRouteSelected(routeLinks.failedMessage.failedMessagesGroups.link) || isRouteSelected(routeLinks.failedMessage.group.link(`id`)), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.failedMessagesGroups.link">
                Failed Message Groups
                <span v-show="failedMessageCount === 0"> (0) </span>
              </RouterLink>
              <span v-if="failedMessageCount !== 0" title="There's varying numbers of failed message groups depending on group type" class="badge badge-important">!</span>
            </h5>

            <!--All Failed Messages-->
            <h5 v-if="!licenseStatus.isExpired" :class="{ active: isRouteSelected(routeLinks.failedMessage.failedMessages.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.failedMessages.link">All Failed Messages </RouterLink>
              <span v-if="failedMessageCount !== 0" class="badge badge-important">{{ failedMessageCount }}</span>
            </h5>

            <!--Deleted Message Group-->
            <h5
              v-if="!licenseStatus.isExpired"
              :class="{ active: isRouteSelected(routeLinks.failedMessage.deletedMessagesGroup.link) || isRouteSelected(routeLinks.failedMessage.deletedGroup.link(`id`)), disabled: !connectionState.connected && !connectionState.connectedRecently }"
            >
              <RouterLink :to="routeLinks.failedMessage.deletedMessagesGroup.link">Deleted Message Groups </RouterLink>
              <span v-if="archivedMessageCount !== 0" title="There's varying numbers of deleted message groups depending on group type" class="badge badge-important">!</span>
            </h5>

            <!--All Deleted Messages-->
            <h5 v-if="!licenseStatus.isExpired" :class="{ active: isRouteSelected(routeLinks.failedMessage.deletedMessages.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.deletedMessages.link">All Deleted Messages </RouterLink>
              <span v-if="archivedMessageCount !== 0" class="badge badge-important">{{ archivedMessageCount }}</span>
            </h5>

            <!--All Pending Retries -->
            <h5 v-if="!licenseStatus.isExpired && showPendingRetry" :class="{ active: isRouteSelected(routeLinks.failedMessage.pendingRetries.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.pendingRetries.link">Pending Retries </RouterLink>
              <span v-if="pendingRetriesMessageCount !== 0" class="badge badge-important">{{ pendingRetriesMessageCount }}</span>
            </h5>
          </div>
        </div>
      </div>
      <RouterView />
    </div>
  </template>
</template>
