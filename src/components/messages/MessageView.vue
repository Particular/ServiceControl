<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import { serviceControlUrl, useFetchFromServiceControl, useTypedFetchFromServiceControl } from "@/composables/serviceServiceControlUrls";
import { useArchiveMessage, useRetryMessages, useUnarchiveMessage } from "@/composables/serviceFailedMessage";
import { useDownloadFileFromString } from "@/composables/fileDownloadCreator";
import { useShowToast } from "@/composables/toast";
import NoData from "../NoData.vue";
import TimeSince from "../TimeSince.vue";
import moment from "moment";
import ConfirmDialog from "../ConfirmDialog.vue";
import FlowDiagram from "./FlowDiagram.vue";
import SequenceDiagram from "./SequenceDiagram.vue";
import EditRetryDialog from "../failedmessages/EditRetryDialog.vue";
import routeLinks from "@/router/routeLinks";
import { EditAndRetryConfig } from "@/resources/Configuration";
import { TYPE } from "vue-toastification";
import { ExtendedFailedMessage, FailedMessageError, FailedMessageStatus, isError } from "@/resources/FailedMessage";
import Message from "@/resources/Message";
import { NServiceBusHeaders } from "@/resources/Header";
import { useConfiguration } from "@/composables/configuration";
import { useIsMassTransitConnected } from "@/composables/useIsMassTransitConnected";
import BodyView from "@/components/messages/BodyView.vue";
import HeadersView from "@/components/messages/HeadersView.vue";
import StackTraceView from "@/components/messages/StacktraceView.vue";
import { stringify, parse } from "lossless-json";
import xmlFormat from "xml-formatter";

let refreshInterval: number | undefined;
let pollingFaster = false;
const panel = ref<number>(1);
const route = useRoute();
const failedMessage = ref<ExtendedFailedMessage | FailedMessageError>();
const editAndRetryConfiguration = ref<EditAndRetryConfig>();

const backLink = ref<string>(routeLinks.failedMessage.failedMessages.link);
const id = computed(() => route.params.id as string);
watch(id, async () => await loadFailedMessage());

const showDeleteConfirm = ref(false);
const showRestoreConfirm = ref(false);
const showRetryConfirm = ref(false);
const showEditRetryModal = ref(false);

const configuration = useConfiguration();
const isMassTransitConnected = useIsMassTransitConnected();
const showAllMessages = window.defaultConfig.showAllMessages;

async function loadFailedMessage() {
  const response = await useFetchFromServiceControl(`errors/last/${id.value}`);
  if (response.status === 404) {
    failedMessage.value = { notFound: true } as FailedMessageError;
    return;
  } else if (!response.ok) {
    failedMessage.value = { error: true } as FailedMessageError;
    return;
  }
  const message = (await response.json()) as ExtendedFailedMessage;
  message.archived = message.status === FailedMessageStatus.Archived;
  message.resolved = message.status === FailedMessageStatus.Resolved;
  message.retried = message.status === FailedMessageStatus.RetryIssued;
  message.error_retention_period = moment.duration(configuration.value?.data_retention.error_retention_period).asHours();
  message.isEditAndRetryEnabled = editAndRetryConfiguration.value?.enabled ?? false;

  // Maintain the mutations of the message in memory until the api returns a newer modified message
  if (failedMessage.value && !isError(failedMessage.value) && failedMessage.value.last_modified === message.last_modified) {
    message.retried = failedMessage.value?.retried;
    message.archiving = failedMessage.value?.archiving;
    message.restoring = failedMessage.value?.restoring;
  } else {
    message.archiving = false;
    message.restoring = false;
  }

  updateMessageDeleteDate(message);
  await fetchMessageDetails(message);
  failedMessage.value = message;
}

async function getEditAndRetryConfig() {
  const [, data] = await useTypedFetchFromServiceControl<EditAndRetryConfig>("edit/config");

  editAndRetryConfiguration.value = data;
}

function updateMessageDeleteDate(message: ExtendedFailedMessage) {
  if (!isError(message)) {
    const countdown = moment(message.last_modified).add(message.error_retention_period, "hours");
    message.delete_soon = countdown < moment();
    message.deleted_in = countdown.format();
  }
}

async function archiveMessage() {
  useShowToast(TYPE.INFO, "Info", `Deleting the message ${id.value} ...`);
  changeRefreshInterval(1000); // We've started an archive, so increase the polling frequency
  await useArchiveMessage([id.value]);
  if (failedMessage.value && !isError(failedMessage.value)) {
    failedMessage.value.archiving = true;
  }
}

async function unarchiveMessage() {
  changeRefreshInterval(1000); // We've started an unarchive, so increase the polling frequency
  await useUnarchiveMessage([id.value]);
  if (failedMessage.value && !isError(failedMessage.value)) {
    failedMessage.value.restoring = true;
  }
}

async function retryMessage() {
  useShowToast(TYPE.INFO, "Info", `Retrying the message ${id.value} ...`);
  changeRefreshInterval(1000); // We've started a retry, so increase the polling frequency
  await useRetryMessages([id.value]);
  if (failedMessage.value && !isError(failedMessage.value)) {
    failedMessage.value.retried = true;
  }
}

async function fetchMessageDetails(message: ExtendedFailedMessage) {
  if (isError(message)) return;

  try {
    const [, data] = await useTypedFetchFromServiceControl<Message[]>(`messages/search/${message.message_id}`);

    const messageDetails = data.find((value) => value.receiving_endpoint.name === message.receiving_endpoint?.name);

    if (!messageDetails) {
      message.headersNotFound = true;
      message.messageBodyNotFound = true;

      return;
    }

    message.headers = messageDetails.headers;
    message.conversationId = messageDetails.headers.find((header) => header.key === NServiceBusHeaders.ConversationId)?.value ?? "";

    await downloadBody(message);
  } catch (err) {
    console.log(err);
    return;
  }
}

async function downloadBody(message: ExtendedFailedMessage) {
  if (isError(message)) return;

  const response = await useFetchFromServiceControl(`messages/${message.message_id}/body`);
  if (response.status === 404) {
    message.messageBodyNotFound = true;

    return;
  }

  try {
    const contentType = response.headers.get("content-type");
    message.contentType = contentType ?? "text/plain";
    message.messageBody = await response.text();

    if (contentType === "application/json") {
      message.messageBody = stringify(parse(message.messageBody), null, 2) ?? message.messageBody;
    }
    if (contentType === "text/xml") {
      message.messageBody = xmlFormat(message.messageBody, { indentation: "  ", collapseContent: true });
    }
  } catch {
    message.bodyUnavailable = true;
  }
}

function togglePanel(panelNum: number) {
  if (failedMessage.value && !isError(failedMessage.value)) {
    panel.value = panelNum;
  }
  return false;
}

function debugInServiceInsight() {
  if (!failedMessage.value || isError(failedMessage.value)) return;

  const messageId = failedMessage.value?.message_id;
  const endpointName = failedMessage.value?.receiving_endpoint.name;
  let url = serviceControlUrl.value?.toLowerCase() ?? "";

  if (url.indexOf("https") === 0) {
    url = url.replace("https://", "");
  } else {
    url = url.replace("http://", "");
  }

  window.open(`si://${url}?search=${messageId}&endpointname=${endpointName}`);
}

function exportMessage() {
  if (!failedMessage.value || isError(failedMessage.value)) return;

  let txtStr = "STACKTRACE\n";
  txtStr += failedMessage.value?.exception.stack_trace;

  txtStr += "\n\nHEADERS";
  if (failedMessage.value) {
    for (let i = 0; i < failedMessage.value.headers.length; i++) {
      txtStr += "\n" + failedMessage.value.headers[i].key + ": " + failedMessage.value.headers[i].value;
    }
  }

  txtStr += "\n\nMESSAGE BODY\n";
  txtStr += failedMessage.value?.messageBody;

  useDownloadFileFromString(txtStr, "text/txt", "failedMessage.txt");
  useShowToast(TYPE.INFO, "Info", "Message export completed.");
}

function showEditAndRetryModal() {
  showEditRetryModal.value = true;
  return stopRefreshInterval();
}

function cancelEditAndRetry() {
  showEditRetryModal.value = false;
  loadFailedMessage(); // Reset the message object when canceling the edit & retry modal
  return startRefreshInterval();
}

function confirmEditAndRetry() {
  showEditRetryModal.value = false;
  useShowToast(TYPE.INFO, "Info", `Retrying the edited message ${id.value} ...`);
  return startRefreshInterval();
}

function startRefreshInterval() {
  stopRefreshInterval(); // clear interval if it exists to prevent memory leaks

  refreshInterval = window.setInterval(() => {
    loadFailedMessage();
  }, 5000);
}

function stopRefreshInterval() {
  if (refreshInterval != null) {
    window.clearInterval(refreshInterval);
  }
}

function isRetryOrArchiveOperationInProgress() {
  if (!failedMessage.value || isError(failedMessage.value)) return false;

  return failedMessage.value?.retried || failedMessage.value?.archiving || failedMessage.value?.restoring;
}

function changeRefreshInterval(milliseconds: number) {
  stopRefreshInterval(); // clear interval if it exists to prevent memory leaks

  refreshInterval = window.setInterval(() => {
    // If we're currently polling at the default interval of 5 seconds and there is a retry, delete, or restore in progress, then change the polling interval
    if (!pollingFaster && isRetryOrArchiveOperationInProgress()) {
      changeRefreshInterval(milliseconds);
      pollingFaster = true;
    } else if (pollingFaster && !isRetryOrArchiveOperationInProgress()) {
      // Reset polling to default value after every retry, delete, and restore. Change polling frequency back to every 5 seconds
      changeRefreshInterval(5000);
      pollingFaster = false;
    }
    loadFailedMessage();
  }, milliseconds);
}

onMounted(async () => {
  const back = useRouter().currentRoute.value.query.back as string;
  if (back) {
    backLink.value = back;
  }
  togglePanel(1);

  await getEditAndRetryConfig();
  startRefreshInterval();
  await loadFailedMessage();
});

onUnmounted(() => {
  stopRefreshInterval();
});
</script>

<template>
  <div v-if="failedMessage" class="container">
    <section>
      <section name="failed_message">
        <no-data
          v-if="isError(failedMessage) && failedMessage.notFound"
          title="message failures"
          message="Could not find message. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl."
        ></no-data>
        <no-data v-if="isError(failedMessage) && failedMessage.error" title="message failures" message="An error occurred while trying to load the message. Please check the ServiceControl logs to learn what the issue is."></no-data>
        <div v-if="!isError(failedMessage)">
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <RouterLink :to="backLink"><i class="fa fa-chevron-left"></i> Back</RouterLink>
              <div class="active break group-title">
                <h1 class="message-type-title">{{ failedMessage.message_type }}</h1>
              </div>
            </div>
          </div>
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <div class="metadata group-message-count message-metadata">
                <span v-if="failedMessage.retried" title="Message is being retried" class="label sidebar-label label-info metadata-label">Retried</span>
                <span v-if="failedMessage.restoring" title="Message is being retried" class="label sidebar-label label-info metadata-label">Restoring...</span>
                <span v-if="failedMessage.archiving" title="Message is being deleted" class="label sidebar-label label-info metadata-label">Deleting...</span>
                <span v-if="failedMessage.archived" title="Message is being deleted" class="label sidebar-label label-warning metadata-label">Deleted</span>
                <span v-if="failedMessage.resolved" title="Message was processed successfully" class="label sidebar-label label-warning metadata-label">Processed</span>
                <span v-if="failedMessage.number_of_processing_attempts > 1" :title="'This message has already failed ' + failedMessage.number_of_processing_attempts + ' times'" class="label sidebar-label label-important metadata-label">
                  {{ failedMessage.number_of_processing_attempts - 1 }} Retry Failures
                </span>
                <span v-if="failedMessage.edited" v-tippy="`Message was edited`" class="label sidebar-label label-info metadata-label">Edited</span>
                <span v-if="failedMessage.edited" class="metadata metadata-link">
                  <i class="fa fa-history"></i> <RouterLink :to="{ path: routeLinks.messages.failedMessage.link(failedMessage.edit_of), query: { back: route.path } }">View previous version</RouterLink>
                </span>
                <span v-if="failedMessage.time_of_failure" class="metadata"><i class="fa fa-clock-o"></i> Failed: <time-since :date-utc="failedMessage.time_of_failure"></time-since></span>
                <span class="metadata"><i class="fa pa-endpoint"></i> Endpoint: {{ failedMessage.receiving_endpoint.name }}</span>
                <span class="metadata"><i class="fa fa-laptop"></i> Machine: {{ failedMessage.receiving_endpoint.host }}</span>
                <span v-if="failedMessage.redirect" class="metadata"><i class="fa pa-redirect-source pa-redirect-small"></i> Redirect: {{ failedMessage.redirect }}</span>
              </div>
              <div class="metadata group-message-count message-metadata" v-if="failedMessage.archived">
                <span class="metadata"><i class="fa fa-clock-o"></i> Deleted: <time-since :date-utc="failedMessage.last_modified"></time-since></span>
                <span class="metadata danger" v-if="failedMessage.delete_soon"><i class="fa fa-trash-o danger"></i> Scheduled for permanent deletion: immediately</span>
                <span class="metadata danger" v-if="!failedMessage.delete_soon"><i class="fa fa-trash-o danger"></i> Scheduled for permanent deletion: <time-since :date-utc="failedMessage.deleted_in"></time-since></span>
              </div>
            </div>
          </div>
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <div class="btn-toolbar message-toolbar">
                <button type="button" class="btn btn-default" v-if="!failedMessage.archived" :disabled="failedMessage.retried || failedMessage.resolved" @click="showDeleteConfirm = true"><i class="fa fa-trash"></i> Delete message</button>
                <button type="button" class="btn btn-default" v-if="failedMessage.archived" @click="showRestoreConfirm = true"><i class="fa fa-undo"></i> Restore</button>
                <button type="button" class="btn btn-default" :disabled="failedMessage.retried || failedMessage.archived || failedMessage.resolved" @click="showRetryConfirm = true"><i class="fa fa-refresh"></i> Retry message</button>
                <button type="button" class="btn btn-default" aria-label="Edit & retry" v-if="failedMessage.isEditAndRetryEnabled" :disabled="failedMessage.retried || failedMessage.archived || failedMessage.resolved" @click="showEditAndRetryModal()">
                  <i class="fa fa-pencil"></i> Edit & retry
                </button>
                <button v-if="!isMassTransitConnected" type="button" class="btn btn-default" @click="debugInServiceInsight()" title="Browse this message in ServiceInsight, if installed">
                  <img src="@/assets/si-icon.svg" alt="ServiceInsight logo" /> View in ServiceInsight
                </button>
                <button type="button" class="btn btn-default" @click="exportMessage()"><i class="fa fa-download"></i> Export message</button>
              </div>
            </div>
          </div>
          <div class="row">
            <div class="col-sm-12 no-side-padding">
              <div class="nav tabs msg-tabs">
                <h5 :class="{ active: panel === 1 }" class="nav-item" @click.prevent="togglePanel(1)"><a href="#">Stacktrace</a></h5>
                <h5 :class="{ active: panel === 2 }" class="nav-item" @click.prevent="togglePanel(2)"><a href="#">Message body</a></h5>
                <h5 :class="{ active: panel === 3 }" class="nav-item" @click.prevent="togglePanel(3)"><a href="#">Headers</a></h5>
                <h5 v-if="!isMassTransitConnected" :class="{ active: panel === 4 }" class="nav-item" @click.prevent="togglePanel(4)"><a href="#">Flow Diagram</a></h5>
                <h5 v-if="showAllMessages" :class="{ active: panel === 5 }" class="nav-item" @click.prevent="togglePanel(5)"><a href="#">Sequence Diagram</a></h5>
              </div>
              <StackTraceView v-if="panel === 1 && failedMessage.exception?.stack_trace" :message="failedMessage" />
              <BodyView v-if="panel === 2" :message="failedMessage" />
              <HeadersView v-if="panel === 3" :message="failedMessage" />
              <FlowDiagram v-if="panel === 4" :message="failedMessage" />
              <SequenceDiagram v-if="showAllMessages && panel === 5" />
            </div>
          </div>

          <!--modal display - restore group-->
          <Teleport to="#modalDisplay">
            <ConfirmDialog
              v-if="showDeleteConfirm === true"
              @cancel="showDeleteConfirm = false"
              @confirm="
                showDeleteConfirm = false;
                archiveMessage();
              "
              heading="Are you sure you want to delete this message?"
              body="If you delete, this message won't be available for retrying unless it is later restored."
            ></ConfirmDialog>

            <ConfirmDialog
              v-if="showRestoreConfirm === true"
              @cancel="showRestoreConfirm = false"
              @confirm="
                showRestoreConfirm = false;
                unarchiveMessage();
              "
              heading="Are you sure you want to restore this message?"
              body="The restored message will be moved back to the list of failed messages."
            ></ConfirmDialog>

            <ConfirmDialog
              v-if="showRetryConfirm === true"
              @cancel="showRetryConfirm = false"
              @confirm="
                showRetryConfirm = false;
                retryMessage();
              "
              heading="Retry Message"
              body="Are you sure you want to retry this message?"
            ></ConfirmDialog>

            <EditRetryDialog
              v-if="editAndRetryConfiguration && failedMessage && showEditRetryModal === true"
              @cancel="cancelEditAndRetry()"
              @retried="confirmEditAndRetry()"
              :id="id"
              :message="failedMessage"
              :configuration="editAndRetryConfiguration"
            ></EditRetryDialog>
          </Teleport>
        </div>
      </section>
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
