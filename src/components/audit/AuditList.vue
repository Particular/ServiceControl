<script setup lang="ts">
import routeLinks from "@/router/routeLinks";
import { ColumnNames, useAuditStore } from "@/stores/AuditStore";
import { storeToRefs } from "pinia";
import SortableColumn from "../SortableColumn.vue";
import { MessageStatus } from "@/resources/Message";
import moment from "moment";
import { useFormatTime } from "@/composables/formatter";
import RefreshConfig from "../RefreshConfig.vue";
import ItemsPerPage from "../ItemsPerPage.vue";
import PaginationStrip from "../PaginationStrip.vue";
import { useRoute } from "vue-router";

const store = useAuditStore();
const { messages, sortByInstances, itemsPerPage, selectedPage, totalCount } = storeToRefs(store);
const route = useRoute();

function statusToName(messageStatus: MessageStatus) {
  switch (messageStatus) {
    case MessageStatus.Successful:
      return "Successful";
    case MessageStatus.ResolvedSuccessfully:
      return "Successful after retries";
    case MessageStatus.Failed:
      return "Failed";
    case MessageStatus.ArchivedFailure:
      return "Failed message deleted";
    case MessageStatus.RepeatedFailure:
      return "Repeated Failures";
    case MessageStatus.RetryIssued:
      return "Retry requested";
  }
}

function statusToIcon(messageStatus: MessageStatus) {
  switch (messageStatus) {
    case MessageStatus.Successful:
      return "fa successful";
    case MessageStatus.ResolvedSuccessfully:
      return "fa resolved-successfully";
    case MessageStatus.Failed:
      return "fa failed";
    case MessageStatus.ArchivedFailure:
      return "fa archived";
    case MessageStatus.RepeatedFailure:
      return "fa repeated-failure";
    case MessageStatus.RetryIssued:
      return "fa retry-issued";
  }
}

function friendlyTypeName(messageType: string) {
  if (messageType == null) return null;

  const typeClass = messageType.split(",")[0];
  const typeName = typeClass.split(".").reverse()[0];
  return typeName.replace(/\+/g, ".");
}

function formatDotNetTimespan(timespan: string) {
  //assuming if we have days in the timespan then something is very, very wrong
  const [hh, mm, ss] = timespan.split(":");
  const time = useFormatTime(((parseInt(hh) * 60 + parseInt(mm)) * 60 + parseFloat(ss)) * 1000);
  return `${time.value} ${time.unit}`;
}
</script>

<template>
  <section class="section-table" role="table" aria-label="endpoint-instances">
    <div class="header">
      <RefreshConfig id="auditListRefresh" @change="store.updateRefreshTimer" @manual-refresh="store.refresh" />
      <!--Table headings-->
      <div role="row" aria-label="column-headers" class="row table-head-row" :style="{ borderTop: 0 }">
        <div role="columnheader" :aria-label="ColumnNames.Status" class="status">
          <SortableColumn :sort-by="ColumnNames.Status" v-model="sortByInstances" :default-ascending="true">Status</SortableColumn>
        </div>
        <div role="columnheader" :aria-label="ColumnNames.MessageId" class="col-3">
          <SortableColumn :sort-by="ColumnNames.MessageId" v-model="sortByInstances" :default-ascending="true">Message Id</SortableColumn>
        </div>
        <div role="columnheader" :aria-label="ColumnNames.MessageType" class="col-3">
          <SortableColumn :sort-by="ColumnNames.MessageType" v-model="sortByInstances" :default-ascending="true">Type</SortableColumn>
        </div>
        <div role="columnheader" :aria-label="ColumnNames.TimeSent" class="col-2">
          <SortableColumn :sort-by="ColumnNames.TimeSent" v-model="sortByInstances">Time Sent</SortableColumn>
        </div>
        <div role="columnheader" :aria-label="ColumnNames.ProcessingTime" class="col-2">
          <SortableColumn :sort-by="ColumnNames.ProcessingTime" v-model="sortByInstances">Processing Time</SortableColumn>
        </div>
      </div>
    </div>
    <!--Table rows-->
    <!--NOTE: currently the DataView pages on the client only: we need to make it server data aware (i.e. the total will be the count from the server, not the length of the data we have locally)-->
    <div class="messages" role="rowgroup" aria-label="messages">
      <div role="row" :aria-label="message.message_id" class="row grid-row" v-for="message in messages" :key="message.id">
        <div role="cell" aria-label="status" class="status" :title="statusToName(message.status)">
          <div class="status-icon" :class="statusToIcon(message.status)"></div>
        </div>
        <div role="cell" aria-label="message-id" class="col-3 message-id">
          <div class="box-header">
            <tippy :aria-label="message.message_id" :delay="[700, 0]" class="no-side-padding lead righ-side-ellipsis endpoint-details-link">
              <template #content>
                <p :style="{ overflowWrap: 'break-word' }">{{ message.message_id }}</p>
              </template>
              <RouterLink
                v-if="message.status === MessageStatus.Successful"
                class="hackToPreventSafariFromShowingTooltip"
                aria-label="details-link"
                :to="{ path: routeLinks.messages.successMessage.link(message.message_id, message.id), query: { back: route.path } }"
              >
                {{ message.message_id }}
              </RouterLink>
              <RouterLink v-else class="hackToPreventSafariFromShowingTooltip" aria-label="details-link" :to="{ path: routeLinks.messages.failedMessage.link(message.id), query: { back: route.path } }">
                {{ message.message_id }}
              </RouterLink>
            </tippy>
          </div>
        </div>
        <div role="cell" aria-label="message-type" class="col-3 message-type">
          {{ friendlyTypeName(message.message_type) }}
        </div>
        <div role="cell" aria-label="time-sent" class="col-2 time-sent">
          {{ moment(message.time_sent).local().format("LLLL") }}
        </div>
        <div role="cell" aria-label="processing-time" class="col-2 processing-time">
          {{ formatDotNetTimespan(message.processing_time) }}
        </div>
      </div>
    </div>
    <div class="row">
      <ItemsPerPage v-model="itemsPerPage" />
      <PaginationStrip v-model="selectedPage" :totalCount="totalCount" :itemsPerPage="itemsPerPage" />
    </div>
  </section>
</template>

<style scoped>
@import "../list.css";

.hackToPreventSafariFromShowingTooltip::after {
  content: "";
  display: block;
}

.section-table {
  overflow: auto;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.messages {
  flex: 1;
  overflow: auto;
}

.status {
  width: 5em;
  text-align: center;
}

.status-icon {
  color: white;
  border-radius: 0.75em;
  width: 1.2em;
  height: 1.2em;
}

.status-icon::before {
  vertical-align: middle;
  font-size: 0.85em;
}

.successful {
  background: #6cc63f;
}
.successful::before {
  content: "\f00c";
}

.resolved-successfully {
  background: #3f881b;
}
.resolved-successfully::before {
  content: "\f01e";
}

.failed {
  background: #c63f3f;
}
.failed::before {
  content: "\f00d";
}

.archived {
  background: #000000;
}
.archived::before {
  content: "\f187";
  font-size: 0.85em;
}

.repeated-failure {
  background: #c63f3f;
}
.repeated-failure::before {
  content: "\f00d\f00d";
  font-size: 0.6em;
}

.retry-issued {
  background: #cccccc;
  color: #000000;
}
.retry-issued::before {
  content: "\f01e";
}

.grid-row {
  display: flex;
  position: relative;
  border-top: 1px solid #eee;
  border-right: 1px solid #fff;
  border-bottom: 1px solid #eee;
  border-left: 1px solid #fff;
  background-color: #fff;
  margin: 0;
}

.grid-row:nth-child(even) {
  background-color: #eee;
}
</style>
