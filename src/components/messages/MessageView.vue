<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { RouteLocationAsPathGeneric, RouterLink, useRoute } from "vue-router";
import NoData from "../NoData.vue";
import TimeSince from "../TimeSince.vue";
import FlowDiagram from "./FlowDiagram/FlowDiagram.vue";
import SequenceDiagram from "./SequenceDiagram.vue";
import routeLinks from "@/router/routeLinks";
import BodyView from "@/components/messages/BodyView.vue";
import HeadersView from "@/components/messages/HeadersView.vue";
import StackTraceView from "@/components/messages/StacktraceView.vue";
import { useMessageStore } from "@/stores/MessageStore";
import DeleteMessageButton from "@/components/messages/DeleteMessageButton.vue";
import RestoreMessageButton from "@/components/messages/RestoreMessageButton.vue";
import RetryMessageButton from "@/components/messages/RetryMessageButton.vue";
import EditAndRetryButton from "@/components/messages/EditAndRetryButton.vue";
import ExportMessageButton from "@/components/messages/ExportMessageButton.vue";
import TabsLayout from "@/components/TabsLayout.vue";
import { storeToRefs } from "pinia";
import MetadataLabel from "@/components/messages/MetadataLabel.vue";
import MetadataItem from "@/components/MetadataItem.vue";
import { hexToCSSFilter } from "hex-to-css-filter";
import SagaDiagram from "./SagaDiagram.vue";
import FAIcon from "@/components/FAIcon.vue";
import { faChevronLeft, faTrash, faLaptop, faClockRotateLeft, faClock } from "@fortawesome/free-solid-svg-icons";
import { useConfigurationStore } from "@/stores/ConfigurationStore";

const route = useRoute();
const id = computed(() => route.params.id as string);
const messageId = computed(() => route.params.messageId as string);
const isError = computed(() => messageId.value === undefined);
const backLink = ref<RouteLocationAsPathGeneric>({ path: routeLinks.failedMessage.failedMessages.link });

const configurationStore = useConfigurationStore();
const { isMassTransitConnected } = storeToRefs(configurationStore);
const store = useMessageStore();
const { state } = storeToRefs(store);

const hasParticipatedInSaga = computed(() => store.state.data.invoked_saga?.has_saga);

const tabs = computed(() => {
  const currentTabs = [
    {
      text: "Message body",
      component: BodyView,
    },
    {
      text: "Headers",
      component: HeadersView,
    },
  ];

  if (isError.value) {
    currentTabs.unshift({
      text: "Stacktrace",
      component: StackTraceView,
    });
  }

  if (!isMassTransitConnected.value) {
    currentTabs.push({
      text: "Flow Diagram",
      component: FlowDiagram,
    });
    currentTabs.push({
      text: "Sequence Diagram",
      component: SequenceDiagram,
    });
    // Add the "Saga Diagram" tab only if the saga has been participated in
    if (hasParticipatedInSaga?.value) {
      currentTabs.push({
        text: "Saga Diagram",
        component: SagaDiagram,
      });
    }
  }

  return currentTabs;
});

watch(
  [id, messageId],
  async ([newId, newMessageId], [oldId, oldMessageId]) => {
    if (newId !== oldId || newMessageId !== oldMessageId) {
      store.reset();
    }

    if (newMessageId !== undefined) {
      await store.loadMessage(newMessageId, newId);
    } else {
      await store.loadFailedMessage(newId);
    }
  },
  { immediate: true }
);
const endpointColor = hexToCSSFilter("#929E9E").filter;

onMounted(() => {
  const { back, ...otherArgs } = route.query;
  if (back) {
    backLink.value = { path: back.toString(), query: otherArgs };
  }
});
</script>

<template>
  <div class="container">
    <section>
      <no-data v-if="state.not_found" title="message failures" message="Could not find message. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl."></no-data>
      <no-data v-else-if="state.failed_to_load" title="message failures" message="An error occurred while trying to load the message. Please check the ServiceControl logs to learn what the issue is."></no-data>
      <template v-else>
        <div class="row">
          <div class="col-sm-12 no-side-padding">
            <RouterLink :to="backLink"><FAIcon :icon="faChevronLeft" size="sm" /> Back</RouterLink>
            <div class="active break group-title">
              <h1 class="message-type-title">{{ state.data.message_type }}</h1>
            </div>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12 no-side-padding">
            <div class="metadata group-message-count message-metadata">
              <MetadataLabel v-if="state.data.failure_status.retry_in_progress" tooltip="Message is being added to the retries queue" type="info" text="Requesting retry..." />
              <MetadataLabel v-if="state.data.failure_status.retried" tooltip="Message is enqueued to be retried" type="info" text="Waiting for retry" />
              <MetadataLabel v-if="state.data.failure_status.restoring" tooltip="Message is being restored" type="info" text="Restoring..." />
              <MetadataLabel v-if="state.data.failure_status.archiving" tooltip="Message is being deleted" type="info" text="Deleting..." />
              <MetadataLabel v-if="state.data.failure_status.archived" tooltip="Message is deleted" type="warning" text="Deleted" />
              <MetadataLabel v-if="state.data.failure_status.resolved" tooltip="Message was processed successfully" type="warning" text="Processed" />
              <MetadataLabel
                v-if="state.data.failure_metadata.number_of_processing_attempts !== undefined && state.data.failure_metadata.number_of_processing_attempts > 1"
                :tooltip="`This message has already failed ${state.data.failure_metadata.number_of_processing_attempts} times`"
                type="important"
                :text="`${(state.data.failure_metadata.number_of_processing_attempts ?? 0) - 1} Retry Failures`"
              />
              <template v-if="state.data.failure_metadata.edited">
                <MetadataLabel tooltip="Message was edited" type="info" text="Edited" />
                <MetadataItem v-if="state.data.failure_metadata.edit_of" :icon="faClockRotateLeft" class="metadata-link">
                  <RouterLink :to="{ path: routeLinks.messages.failedMessage.link(state.data.failure_metadata.edit_of), query: route.query }">View previous version</RouterLink>
                </MetadataItem>
              </template>
              <MetadataItem v-if="state.data.failure_metadata.time_of_failure" :icon="faClock">Failed: <time-since :date-utc="state.data.failure_metadata.time_of_failure"></time-since></MetadataItem>
              <MetadataItem v-else :icon="faClock">Processed at: <time-since :date-utc="state.data.processed_at"></time-since></MetadataItem>
              <template v-if="state.data.receiving_endpoint">
                <span class="metadata"><i class="fa pa-endpoint" :style="{ filter: endpointColor }"></i> Endpoint: {{ state.data.receiving_endpoint.name }}</span>
                <MetadataItem :icon="faLaptop">Machine: {{ state.data.receiving_endpoint.host }}</MetadataItem>
              </template>
              <span v-if="state.data.failure_metadata.redirect" class="metadata"><i class="fa pa-redirect-source pa-redirect-small"></i> Redirect: {{ state.data.failure_metadata.redirect }}</span>
              <template v-if="state.data.failure_status.archived">
                <MetadataItem :icon="faClock">Deleted: <time-since :date-utc="state.data.failure_metadata.last_modified"></time-since></MetadataItem>
                <MetadataItem v-if="state.data.failure_status.delete_soon" :icon="faTrash" class="danger">Scheduled for permanent deletion: immediately</MetadataItem>
                <MetadataItem v-else :icon="faTrash" class="danger">Scheduled for permanent deletion: <time-since :date-utc="state.data.failure_metadata.deleted_in"></time-since></MetadataItem>
              </template>
            </div>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12 no-side-padding">
            <div class="btn-toolbar message-toolbar">
              <DeleteMessageButton />
              <RestoreMessageButton />
              <RetryMessageButton />
              <EditAndRetryButton />
              <ExportMessageButton />
            </div>
          </div>
        </div>
        <div class="row">
          <div class="col-sm-12 no-side-padding">
            <TabsLayout :tabs="tabs" />
          </div>
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
@import "../list.css";

h1.message-type-title {
  margin: 0 0 8px;
  font-size: 24px;
}

.message-metadata {
  display: flex;
}

div.btn-toolbar.message-toolbar {
  margin-bottom: 20px;
}

button img {
  position: relative;
  top: -1px;
  width: 17px;
}

.pa-redirect-source {
  background-image: url("@/assets/redirect-source.svg");
  background-position: center;
  background-repeat: no-repeat;
}

.pa-redirect-small {
  position: relative;
  top: 1px;
  height: 14px;
  width: 14px;
}

.pa-endpoint {
  position: relative;
  top: 3px;
  background-image: url("@/assets/endpoint.svg");
  background-position: center;
  background-repeat: no-repeat;
  display: inline-block;
  height: 15px;
  width: 15px;
}
</style>
