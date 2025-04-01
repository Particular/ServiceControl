<script setup lang="ts">
import { useRouter } from "vue-router";
import TimeSince from "../TimeSince.vue";
import NoData from "../NoData.vue";
import routeLinks from "@/router/routeLinks";
import { FailedMessageStatus, ExtendedFailedMessage } from "@/resources/FailedMessage";

export interface IMessageList {
  getSelectedMessages(): ExtendedFailedMessage[];
  selectAll(): void;
  deselectAll(): void;
  resolveAll(): void;
  isAnythingSelected(): ExtendedFailedMessage | undefined;
  isAnythingDisplayed(): boolean;
  numberDisplayed(): number;
}

let lastLabelClickedIndex: number | undefined;
const router = useRouter();
const emit = defineEmits(["retryRequested"]);
const props = withDefaults(
  defineProps<{
    messages: ExtendedFailedMessage[];
    showRequestRetry?: boolean;
  }>(),
  { showRequestRetry: false }
);

function getSelectedMessages() {
  return props.messages.filter((m) => m.selected);
}

function selectAll() {
  props.messages.forEach((m) => (m.selected = true));
}

function deselectAll() {
  props.messages.forEach((m) => (m.selected = false));
}

function resolveAll() {
  props.messages.forEach((m) => (m.resolved = true));
}

function isAnythingSelected() {
  return props.messages.find((m) => m.selected);
}

function isAnythingDisplayed() {
  return props.messages.length > 0;
}

function numberDisplayed() {
  return props.messages.length;
}

function labelClicked($event: MouseEvent, index: number) {
  //TODO: this functionality isn't consistent on including start/end items
  if ($event.shiftKey && lastLabelClickedIndex != null) {
    // toggle selection from lastLabel until current index
    const start = (index < lastLabelClickedIndex ? index : lastLabelClickedIndex) + 1;
    const end = (index < lastLabelClickedIndex ? lastLabelClickedIndex : index) + 1;

    const messages = props.messages;
    for (let x = start; x < end; x++) {
      messages[x].selected = !messages[x].selected;
    }

    //clear selection
    const selection = document.getSelection();
    if (selection) {
      selection.empty();
    }
  }

  lastLabelClickedIndex = index;
}

function navigateToMessage(messageId: string) {
  router.push({ path: routeLinks.messages.failedMessage.link(messageId) });
}

defineExpose<IMessageList>({
  getSelectedMessages,
  selectAll,
  deselectAll,
  resolveAll,
  isAnythingSelected,
  isAnythingDisplayed,
  numberDisplayed,
});
</script>

<template>
  <div class="row">
    <div class="col-sm-12">
      <no-data v-if="props.messages.length === 0" title="message" message="There are currently no messages"></no-data>
    </div>
  </div>
  <div v-for="(message, index) in props.messages" class="row box repeat-item failed-message" :key="message.id">
    <label class="check col-auto" :for="`checkbox${message.id}`" @click="labelClicked($event, index)">
      <input type="checkbox" :disabled="message.retryInProgress || message.submittedForRetrial || message.deleteInProgress || message.restoreInProgress" class="checkbox" v-model="message.selected" :value="message.id" :id="`checkbox${message.id}`" />
    </label>
    <div class="col-11 failed-message-data">
      <div class="row">
        <div class="col-12">
          <div class="row box-header">
            <div class="col-12 no-side-padding" @click="navigateToMessage(message.id)">
              <p class="lead break">{{ message.message_type || "Message Type Unknown - missing metadata EnclosedMessageTypes" }}</p>
              <p class="metadata">
                <span v-if="message.submittedForRetrial" :title="'Message was submitted for retrying'" class="label sidebar-label label-info metadata-label">To retry</span>
                <span v-if="message.retryInProgress" :title="'Message is being retried'" class="label sidebar-label label-info metadata-label metadata in-progress"><i class="bi-arrow-clockwise"></i> Retry in progress</span>
                <span v-if="message.retried" :title="'Message is being retried'" class="label sidebar-label label-info metadata-label metadata in-progress"><i class="bi-arrow-clockwise"></i> Retried</span>
                <span v-if="message.resolved" class="label sidebar-label label-info metadata-label">Resolved</span>

                <span v-if="message.deleteInProgress" :title="'Message is being deleted'" class="label sidebar-label label-info metadata-label metadata in-progress"><i class="bi-trash"></i> Scheduled for deletion</span>
                <span v-if="message.archived" :title="'Message is being deleted'" class="label sidebar-label label-info metadata-label metadata in-progress"><i class="bi-trash"></i> Deleted</span>
                <span v-if="message.number_of_processing_attempts > 1" :title="`This message has already failed ${message.number_of_processing_attempts} times`" class="label sidebar-label label-important metadata-label"
                  >{{ message.number_of_processing_attempts === 10 ? "9+" : message.number_of_processing_attempts - 1 }} Retry Failures</span
                >
                <span v-if="message.restoreInProgress" v-tippy="`Message is being restored`" class="label sidebar-label label-warning metadata-label metadata in-progress"><i class="bi-recycle"></i> Restore in progress</span>
                <span v-if="message.edited" :title="'Message was edited'" class="label sidebar-label label-info metadata-label">Edited</span>

                <span class="metadata"><i class="fa fa-clock-o"></i> Failed: <time-since :dateUtc="message.time_of_failure"></time-since></span>
                <span class="metadata"><i class="fa pa-endpoint"></i> Endpoint: {{ message.receiving_endpoint.name }}</span>
                <span class="metadata"><i class="fa fa-laptop"></i> Machine: {{ message.receiving_endpoint.host }}</span>
                <span class="metadata" v-if="message.redirect"><i class="fa pa-redirect-source pa-redirect-small"></i> Redirect: {{ message.redirect }}</span>
                <!-- for deleted messages-->
                <span class="metadata" v-if="message.status === FailedMessageStatus.Archived"><i class="fa fa-clock-o"></i> Deleted: <time-since :date-utc="message.last_modified"></time-since></span>
                <span class="metadata danger" v-if="message.status === FailedMessageStatus.Archived && message.delete_soon"><i class="fa fa-trash-o danger"></i> Scheduled for deletion: immediately</span>
                <span class="metadata danger" v-if="message.status === FailedMessageStatus.Archived && !message.delete_soon">
                  <i class="fa fa-trash-o danger"></i> Scheduled for deletion: <time-since class="danger" :date-utc="message.deleted_in"></time-since>
                </span>

                <button type="button" name="retryMessage" v-if="!message.retryInProgress && props.showRequestRetry" class="btn btn-link btn-sm" @click.stop="emit('retryRequested', message.id)">
                  <i aria-hidden="true" class="fa fa-repeat no-link-underline">&nbsp;</i>Request retry
                </button>
              </p>

              <pre class="stacktrace-preview">{{ message.exception.message }}</pre>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@import "../list.css";

.stacktrace-preview {
  height: 38px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

pre {
  display: block;
  padding: 9.5px;
  margin: 0 0 10px;
  font-size: 13px;
  line-height: 1.42857143;
  color: #333333;
  word-break: break-all;
  word-wrap: break-word;
  background-color: #f5f5f5;
  border: 1px solid #ccc;
  border-radius: 4px;
}

.failed-message:hover {
  border: 1px solid #00a3c4;
  background-color: #edf6f7;
}

.repeat-item {
  padding: 0 !important;
}

.check,
div.failed-message-data {
  padding-top: 15px;
  padding-left: 25px;
  padding-bottom: 0;
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

.checkbox {
  margin-top: 1px;
  margin-left: 1px;
  width: 16px;
  height: 16px;
  border: 1px solid #929e9e;
  background-color: #fff;
}

.checkbox:hover,
.check-hover {
  margin-top: 0;
  margin-left: 0;
  width: 18px;
  height: 18px;
  border: 2px solid #00a3c4;
}

label:after .checkbox {
  opacity: 0;
  content: "";
  position: absolute;
  width: 11px;
  height: 7px;
  background: transparent;
  top: 2px;
  left: 2px;
  border: 3px solid #333;
  border-top: none;
  border-right: none;
  transform: rotate(-45deg);
}

.checkbox input[type="checkbox"]:checked + label:after {
  opacity: 1;
}

p.metadata {
  margin-bottom: 6px;
  position: relative;
}

p.metadata button {
  position: absolute;
  right: 0px;
  top: 0;
}

.failed-message .btn-link,
.failed-message-group .btn-link,
.deleted-message-group .btn-link {
  color: #00a3c4;
  font-size: 14px;
  font-weight: bold;
  padding: 0 36px 10px 0;
  text-decoration: none;
}

.failed-message .metadata > .btn-link {
  display: none;
}

.failed-message:hover .metadata > .btn-link {
  display: block;
}

.failed-message .btn-link:hover,
.failed-message-group .btn-link:hover,
.deleted-message-group .btn-link:hover {
  color: #23527c;
  text-decoration: underline;
}

.label-info {
  background-color: #1b809e;
}

.metadata-label {
  margin-right: 24px;
  position: relative;
  top: -1px;
}

.sidebar-label,
.sidebar-label.label-important {
  box-shadow: none;
  color: #ffffff;
  display: inline-block;
  font-size: 12px;
  margin-top: 3px;
  max-width: 100%;
  padding: 6px 10px;
}

.label {
  display: inline;
  padding: 4px 0.6em 0.3em;
  font-size: 13px;
  font-weight: 700;
  line-height: 1;
  color: #fff;
  text-align: center;
  white-space: nowrap;
  vertical-align: baseline;
  border-radius: 0.25em;
}

.failed-message-data,
.failed-message-group,
.deleted-message-group {
  cursor: pointer;
}

.failed-message-data:hover .lead.break,
.failed-message-group:hover .lead.break,
.deleted-message-group:hover .lead.break {
  text-decoration: underline;
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

.btn.btn-sm {
  color: #00a3c4;
  font-size: 14px;
  font-weight: bold;
  padding: 0 36px 10px 0;
}
</style>
