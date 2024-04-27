<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
import { licenseStatus } from "../composables/serviceLicense";
import { connectionState, stats } from "../composables/serviceServiceControl";
import LicenseExpired from "../components/LicenseExpired.vue";
import routeLinks from "@/router/routeLinks";
import isRouteSelected from "@/composables/isRouteSelected";

const showPendingRetry = window.defaultConfig.showPendingRetry;
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
                <span v-show="stats.number_of_failed_messages === 0"> (0) </span>
              </RouterLink>
              <span v-if="stats.number_of_failed_messages !== 0" title="There's varying numbers of failed message groups depending on group type" class="badge badge-important">!</span>
            </h5>

            <!--All Failed Messages-->
            <h5
              v-if="!licenseStatus.isExpired"
              :class="{ active: isRouteSelected(routeLinks.failedMessage.failedMessages.link) || isRouteSelected(routeLinks.failedMessage.message.link(`id`)), disabled: !connectionState.connected && !connectionState.connectedRecently }"
            >
              <RouterLink :to="routeLinks.failedMessage.failedMessages.link">All Failed Messages </RouterLink>
              <span v-if="stats.number_of_failed_messages !== 0" class="badge badge-important">{{ stats.number_of_failed_messages }}</span>
            </h5>

            <!--Deleted Message Group-->
            <h5
              v-if="!licenseStatus.isExpired"
              :class="{ active: isRouteSelected(routeLinks.failedMessage.deletedMessagesGroup.link) || isRouteSelected(routeLinks.failedMessage.deletedGroup.link(`id`)), disabled: !connectionState.connected && !connectionState.connectedRecently }"
            >
              <RouterLink :to="routeLinks.failedMessage.deletedMessagesGroup.link">Deleted Message Groups </RouterLink>
              <span v-if="stats.number_of_archived_messages !== 0" title="There's varying numbers of deleted message groups depending on group type" class="badge badge-important">!</span>
            </h5>

            <!--All Deleted Messages-->
            <h5 v-if="!licenseStatus.isExpired" :class="{ active: isRouteSelected(routeLinks.failedMessage.deletedMessages.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.deletedMessages.link">All Deleted Messages </RouterLink>
              <span v-if="stats.number_of_archived_messages !== 0" class="badge badge-important">{{ stats.number_of_archived_messages }}</span>
            </h5>

            <!--All Pending Retries -->
            <h5 v-if="!licenseStatus.isExpired && showPendingRetry" :class="{ active: isRouteSelected(routeLinks.failedMessage.pendingRetries.link), disabled: !connectionState.connected && !connectionState.connectedRecently }">
              <RouterLink :to="routeLinks.failedMessage.pendingRetries.link">Pending Retries </RouterLink>
              <span v-if="stats.number_of_pending_retries !== 0" class="badge badge-important">{{ stats.number_of_pending_retries }}</span>
            </h5>
          </div>
        </div>
      </div>
      <RouterView />
    </div>
  </template>
</template>

<style>
.panel-retry {
  background-color: #1a1a1a;
  border: none;
  color: #fff;
}

.panel-retry p.lead {
  color: #fff;
}

.navbar-inverse {
  background-color: #1a1a1a;
}

.panel-retry span.metadata,
.panel-retry sp-moment {
  color: #b0b5b5 !important;
}

li.active div.bulk-retry-progress-status:before {
  font: normal normal normal 14px/1 FontAwesome;
  content: "\f061 \00a0";
}

div.retry-completed.bulk-retry-progress-status {
  color: #fff;
  font-weight: bold;
}

li.completed div.bulk-retry-progress-status:before,
div.retry-completed.bulk-retry-progress-status:before {
  font: normal normal normal 14px/1 FontAwesome;
  content: "\f00c \00a0";
}

div.col-xs-3.col-sm-3.retry-op-queued {
  color: #b0b5b5 !important;
}

div.progress-bar.progress-bar-striped.active {
  color: #fff !important;
}

.progress.bulk-retry-progress {
  margin-bottom: 0;
  background-color: #333333;
}

.retry-completed,
ul.retry-request-progress button {
  display: inline-block;
}

ul.retry-request-progress button,
.monitoring-no-data button {
  background-color: #00a3c4;
}

li.left-to-do,
li.completed {
  color: #b0b5b5;
}

li.left-to-do {
  padding-left: 15px;
}

ul.retry-request-progress li > div {
  margin-bottom: 6px;
}

.btn.btn-sm {
  color: #00a3c4;
  font-size: 14px;
  font-weight: bold;
  padding: 0 36px 10px 0;
}

.panel {
  margin-bottom: 20px;
  border: 1px solid transparent;
  border-radius: 4px;
  -webkit-box-shadow: 0 1px 1px rgba(0, 0, 0, 0.05);
  box-shadow: 0 1px 1px rgba(0, 0, 0, 0.05);
}

.panel-body {
  padding: 15px;
}

.panel-body ul {
  list-style: none;
  padding-left: 0;
}

.panel-body ul {
  list-style: none;
}

.op-metadata {
  border-top: 1px solid #414242;
  padding-top: 15px;
}

.retry-request-progress .row {
  padding-left: 13px;
}

.note {
  margin-bottom: 10px;
  background-color: #fcf8e3;
  border: 1px solid #faebcc;
  padding: 10px 15px;
}

.metadata.danger,
.metadata.danger > .danger {
  font-weight: normal !important;
}
</style>
