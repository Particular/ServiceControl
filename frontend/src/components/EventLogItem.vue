<script setup lang="ts">
import { useRouter } from "vue-router";
import TimeSince from "../components/TimeSince.vue";
import type EventLogItem from "@/resources/EventLogItem";
// eslint-disable-next-line no-duplicate-imports
import { Severity } from "@/resources/EventLogItem";
import routeLinks from "@/router/routeLinks";
import FAIcon from "@/components/FAIcon.vue";
import { faCheck, faEnvelope, faExclamation, faHeartbeat, faPencil, faPlus, faTimes, faTrash } from "@fortawesome/free-solid-svg-icons";
import { computed } from "vue";

const props = defineProps<{ eventLogItem: EventLogItem }>();
const router = useRouter();

function navigateToEvent() {
  switch (props.eventLogItem.category) {
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
      if (props.eventLogItem.related_to?.length && props.eventLogItem.related_to[0].search("message") > 0) {
        const messageId = props.eventLogItem.related_to[0].substring(9);
        router.push({ path: routeLinks.messages.failedMessage.link(messageId) });
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

const icon = computed(() => {
  switch (props.eventLogItem.category) {
    case "Endpoints":
    case "EndpointControl":
    case "HeartbeatMonitoring":
      return faHeartbeat;
    case "CustomChecks":
      return faCheck;
    case "MessageFailures":
    case "Recoverability":
      return faEnvelope;
    case "ExternalIntegrations":
      return faExclamation;
    default:
      return null;
  }
});

const subIcon = computed(() => {
  if (props.eventLogItem.severity === Severity.Error) {
    return faTimes;
  } else if (props.eventLogItem.category === "MessageRedirects") {
    switch (props.eventLogItem.event_type) {
      case "MessageRedirectChanged":
        return faPencil;
      case "MessageRedirectCreated":
        return faPlus;
      case "MessageRedirectRemoved":
        return faTrash;
    }
  }
  return null;
});
</script>

<template>
  <div class="row box box-event-item">
    <div class="col-12" @click="navigateToEvent">
      <div class="row">
        <div class="col-auto col-icon">
          <FAIcon v-if="icon" class="icon" :class="{ danger: props.eventLogItem.severity === Severity.Error }" :icon="icon" size="2x" />
          <i v-else class="icon pa-redirect-source pa-redirect-large" />
          <FAIcon v-if="subIcon" class="icon sub-item" :class="{ danger: eventLogItem.severity === Severity.Error }" :icon="subIcon" />
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

.box {
  padding-bottom: 0;
}

.box:hover {
  cursor: pointer;
  background-color: #edf6f7;
  border: 1px solid var(--sp-blue);
}

.row.box-event-item,
.row.box-event-item .col-xs-12,
.row.box.box-event-item .col-12 {
  padding-top: 0.5em;
  padding-bottom: 0.3em;
  width: 100%;
}

.col-icon {
  width: 4.5rem;
}

.col-message {
  display: table-cell;
  width: auto;
  vertical-align: middle;
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

.pa-redirect-source {
  background-image: url("@/assets/redirect-source.svg");
  background-position: center;
  background-repeat: no-repeat;
}

.pa-redirect-large {
  height: 24px;
  width: 24px;
}

.icon {
  color: var(--reduced-emphasis);
}

.sub-item {
  position: relative;
  bottom: -0.5rem;
}
</style>
