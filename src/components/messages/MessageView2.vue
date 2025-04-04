<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { RouterLink, useRoute } from "vue-router";
import NoData from "../NoData.vue";
import TimeSince from "../TimeSince.vue";
import FlowDiagram from "./FlowDiagram.vue";
import routeLinks from "@/router/routeLinks";
import { useIsMassTransitConnected } from "@/composables/useIsMassTransitConnected";
import BodyView from "@/components/messages/BodyView.vue";
import HeadersView from "@/components/messages/HeadersView.vue";
import StackTraceView from "@/components/messages/StacktraceView.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import DeleteMessageButton from "@/components/messages/DeleteMessageButton.vue";
import RestoreMessageButton from "@/components/messages/RestoreMessageButton.vue";
import RetryMessageButton from "@/components/messages/RetryMessageButton.vue";
import EditAndRetryButton from "@/components/messages/EditAndRetryButton.vue";
import ExportMessageButton from "@/components/messages/ExportMessageButton.vue";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import { storeToRefs } from "pinia";
import MetadataLabel from "@/components/messages/MetadataLabel.vue";

const panel = ref<number>(1);
const route = useRoute();
const id = computed(() => route.params.id as string);
const messageId = computed(() => route.params.messageId as string);
const isError = computed(() => messageId.value === undefined);

const isMassTransitConnected = useIsMassTransitConnected();
const store = useMessageViewStore();
const { state } = storeToRefs(store);

function togglePanel(panelNum: number) {
  panel.value = panelNum;
}

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

onMounted(() => {
  togglePanel(isError.value ? 1 : 2);
});
</script>

<template>
  <div class="container">
    <section>
      <no-data v-if="state.not_found" title="message failures" message="Could not find message. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl."></no-data>
      <no-data v-else-if="state.failed_to_load" title="message failures" message="An error occurred while trying to load the message. Please check the ServiceControl logs to learn what the issue is."></no-data>
      <template v-else>
        <LoadingSpinner v-if="state.loading ?? false" />
        <template v-else>
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <div class="active break group-title">
                <h1 class="message-type-title">{{ state.data.message_type }}</h1>
              </div>
            </div>
          </div>
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <div class="metadata group-message-count message-metadata">
                <MetadataLabel v-if="state.data.failure_status.retried" tooltip="Message is being retried" type="info" text="Retried" />
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
                  <span v-if="state.data.failure_metadata.edit_of" class="metadata metadata-link">
                    <i class="fa fa-history"></i> <RouterLink :to="{ path: routeLinks.messages.failedMessage.link(state.data.failure_metadata.edit_of) }">View previous version</RouterLink>
                  </span>
                </template>
                <span v-if="state.data.failure_metadata.time_of_failure" class="metadata"><i class="fa fa-clock-o"></i> Failed: <time-since :date-utc="state.data.failure_metadata.time_of_failure"></time-since></span>
                <span v-else class="metadata"><i class="fa fa-clock-o"></i> Processed at: <time-since :date-utc="state.data.processed_at"></time-since></span>
                <template v-if="state.data.receiving_endpoint">
                  <span class="metadata"><i class="fa pa-endpoint"></i> Endpoint: {{ state.data.receiving_endpoint.name }}</span>
                  <span class="metadata"><i class="fa fa-laptop"></i> Machine: {{ state.data.receiving_endpoint.host }}</span>
                </template>
                <span v-if="state.data.failure_metadata.redirect" class="metadata"><i class="fa pa-redirect-source pa-redirect-small"></i> Redirect: {{ state.data.failure_metadata.redirect }}</span>
                <template v-if="state.data.failure_status.archived">
                  <span class="metadata"><i class="fa fa-clock-o"></i> Deleted: <time-since :date-utc="state.data.failure_metadata.last_modified"></time-since></span>
                  <span class="metadata danger" v-if="state.data.failure_status.delete_soon"><i class="fa fa-trash-o danger"></i> Scheduled for permanent deletion: immediately</span>
                  <span class="metadata danger" v-else><i class="fa fa-trash-o danger"></i> Scheduled for permanent deletion: <time-since :date-utc="state.data.failure_metadata.deleted_in"></time-since></span>
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
              <div class="nav tabs msg-tabs">
                <h5 v-if="isError" :class="{ active: panel === 1 }" class="nav-item" @click.prevent="togglePanel(1)"><a href="#">Stacktrace</a></h5>
                <h5 :class="{ active: panel === 2 }" class="nav-item" @click.prevent="togglePanel(2)"><a href="#">Message body</a></h5>
                <h5 :class="{ active: panel === 3 }" class="nav-item" @click.prevent="togglePanel(3)"><a href="#">Headers</a></h5>
                <h5 v-if="!isMassTransitConnected" :class="{ active: panel === 4 }" class="nav-item" @click.prevent="togglePanel(4)"><a href="#">Flow Diagram</a></h5>
              </div>
              <StackTraceView v-if="isError && panel === 1" />
              <BodyView v-if="panel === 2" />
              <HeadersView v-if="panel === 3" />
              <FlowDiagram v-if="panel === 4" />
            </div>
          </div>
        </template>
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
  display: inline;
}

div.btn-toolbar.message-toolbar {
  margin-bottom: 20px;
}

button img {
  position: relative;
  top: -1px;
  width: 17px;
}

.msg-tabs {
  margin-bottom: 20px;
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
  height: 15px;
  width: 15px;
}
</style>
