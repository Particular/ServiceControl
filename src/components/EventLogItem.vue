<script setup lang="ts">
import { useRouter } from "vue-router";
import TimeSince from "../components/TimeSince.vue";
import type EventLogItem from "@/resources/EventLogItem";
// eslint-disable-next-line no-duplicate-imports
import { Severity } from "@/resources/EventLogItem";
import routeLinks from "@/router/routeLinks";

defineProps<{ eventLogItem: EventLogItem }>();
const router = useRouter();

function navigateToEvent(eventLogItem: EventLogItem) {
  switch (eventLogItem.category) {
    case "Endpoints":
      router.push(routeLinks.configuration.endpointConnection.link);
      break;
    case "HeartbeatMonitoring":
      router.push(routeLinks.heartbeats.root);
      break;
    case "CustomChecks":
      router.push(routeLinks.customChecks);
      break;
    case "EndpointControl":
      router.push(routeLinks.heartbeats.root);
      break;
    case "MessageFailures":
      if (eventLogItem.related_to?.length && eventLogItem.related_to[0].search("message") > 0) {
        const messageId = eventLogItem.related_to[0].substring(9);
        router.push(routeLinks.failedMessage.message.link(messageId));
      } else {
        router.push(routeLinks.failedMessage.root);
      }
      break;
    case "Recoverability":
      router.push(routeLinks.failedMessage.root);
      break;
    case "MessageRedirects":
      router.push(routeLinks.configuration.retryRedirects.link);
      break;
    default:
  }
}

function iconClasses(eventItem: EventLogItem) {
  return {
    normal: eventItem.severity === Severity.Info,
    danger: eventItem.severity === Severity.Error,
    "fa-heartbeat": eventItem.category === "Endpoints" || eventItem.category === "EndpointControl" || eventItem.category === "HeartbeatMonitoring",
    "fa-check": eventItem.category === "CustomChecks",
    "fa-envelope": eventItem.category === "MessageFailures" || eventItem.category === "Recoverability",
    "pa-redirect-source pa-redirect-large": eventItem.category === "MessageRedirects",
    "fa-exclamation": eventItem.category === "ExternalIntegrations",
  };
}

function iconSubClasses(eventItem: EventLogItem) {
  return {
    "fa-times fa-error": (eventItem.severity === Severity.Error || eventItem.category === "MessageRedirects") && eventItem.severity === Severity.Error,
    "fa-pencil": (eventItem.severity === Severity.Error || eventItem.category === "MessageRedirects") && eventItem.category === "MessageRedirects" && eventItem.event_type === "MessageRedirectChanged",
    "fa-plus": (eventItem.severity === Severity.Error || eventItem.category === "MessageRedirects") && eventItem.category === "MessageRedirects" && eventItem.event_type === "MessageRedirectCreated",
    "fa-trash": (eventItem.severity === Severity.Error || eventItem.category === "MessageRedirects") && eventItem.category === "MessageRedirects" && eventItem.event_type === "MessageRedirectRemoved",
  };
}
</script>

<template>
  <div class="row box box-event-item">
    <div class="col-12" @click="navigateToEvent(eventLogItem)">
      <div class="row">
        <div class="col-auto">
          <span class="fa-stack fa-lg">
            <i class="fa fa-stack-2x" :class="iconClasses(eventLogItem)" />
            <i v-if="eventLogItem.severity === Severity.Error || eventLogItem.category === 'MessageRedirects'" class="fa fa-o fa-stack-1x fa-inverse" :class="iconSubClasses(eventLogItem)" />
          </span>
        </div>
        <div class="col-9">
          <div class="row box-header">
            <div class="col-12">
              <p class="lead">
                {{ eventLogItem.description }}
              </p>
            </div>
          </div>
        </div>
        <div class="col-2">
          <time-since :date-utc="eventLogItem.raised_at"></time-since>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "./list.css";

.fa-stack-2x {
  font-size: 1.5em;
}

.box {
  padding-bottom: 0;
}

.box:hover {
  cursor: pointer;
  background-color: #edf6f7;
  border: 1px solid #00a3c4;
}

.row.box-event-item,
.row.box-event-item .col-xs-12,
.row.box.box-event-item .col-12 {
  padding-top: 0.5em;
  padding-bottom: 0.3em;
  width: 100%;
}

.col-icon {
  display: table-cell;
  width: 5em;
  vertical-align: middle;
}

.col-message {
  display: table-cell;
  width: auto;
  vertical-align: middle;
}

.col-icon .fa-stack {
  top: -0.5em;
}

.col-message p.lead {
  padding-bottom: 0.125em;
}

.col-timestamp {
  display: table-cell;
  width: 8em;
  vertical-align: middle;
  padding-top: 0;
  padding-bottom: 0.125em;
}

.box {
  box-shadow: none;
  margin: 0 !important;
  padding-bottom: 0.625em;
}

.box-event-item .fa-stack {
  height: 1em;
}

.pa-redirect-source {
  background-image: url("@/assets/redirect-source.svg");
  background-position: center;
  background-repeat: no-repeat;
}

.pa-redirect-large {
  height: 24px;
}
</style>
