<script setup lang="ts">
import { SagaUpdateViewModel } from "./SagaDiagramParser";
import MessageDataBox from "./MessageDataBox.vue";
import SagaOutgoingTimeoutMessage from "./SagaOutgoingTimeoutMessage.vue";
import SagaOutgoingMessage from "./SagaOutgoingMessage.vue";

// Import the images directly
import CommandIcon from "@/assets/command.svg";
import SagaInitiatedIcon from "@/assets/SagaInitiatedIcon.svg";
import SagaUpdatedIcon from "@/assets/SagaUpdatedIcon.svg";
import TimeoutIcon from "@/assets/timeout.svg";
import SagaTimeoutIcon from "@/assets/SagaTimeoutIcon.svg";
defineProps<{
  update: SagaUpdateViewModel;
  showMessageData?: boolean;
}>();
</script>

<template>
  <div class="block" role="row">
    <!-- Initiating message and saga status header -->
    <div class="row">
      <div class="cell cell--side">
        <div class="cell-inner cell-inner-side">
          <img class="saga-icon saga-icon--side-cell" :src="update.InitiatingMessage.IsSagaTimeoutMessage ? TimeoutIcon : CommandIcon" alt="" />
          <h2 class="message-title" aria-label="initiating message type">{{ update.InitiatingMessage.MessageType }}</h2>
          <div class="timestamp" aria-label="initiating message timestamp">{{ update.InitiatingMessage.FormattedMessageTimestamp }}</div>
        </div>
      </div>
      <div class="cell cell--center cell-flex">
        <div class="cell-inner cell-inner-center cell-inner--align-bottom">
          <template v-if="update.InitiatingMessage.IsSagaTimeoutMessage">
            <img class="saga-icon saga-icon--center-cell" :src="SagaTimeoutIcon" alt="" />
            <h2 class="saga-status-title saga-status-title--inline timeout-status" aria-label="timeout invoked">Timeout Invoked</h2>
            <br />
          </template>
          <img class="saga-icon saga-icon--center-cell" :src="update.IsFirstNode ? SagaInitiatedIcon : SagaUpdatedIcon" alt="" />
          <h2 class="saga-status-title saga-status-title--inline">{{ update.StatusDisplay }}</h2>
          <div class="timestamp timestamp--inline" aria-label="time stamp">{{ update.FormattedStartTime }}</div>
        </div>
      </div>
    </div>

    <!-- Saga properties and outgoing messages -->
    <div class="row">
      <!-- Left side - Message Data box -->
      <div class="cell cell--side cell--left-border cell--aling-top">
        <div v-if="showMessageData" class="message-data message-data--active">
          <!-- Generic message data box -->
          <MessageDataBox v-if="update.InitiatingMessage.MessageType" :messageData="update.InitiatingMessage.MessageData" />
        </div>
      </div>

      <!-- Center - Saga properties -->
      <div class="cell cell--center cell--center--border">
        <div :class="{ 'cell-inner': true, 'cell-inner-line': update.HasTimeout, 'cell-inner-center': !update.HasTimeout }">
          <div class="saga-properties">
            <a class="saga-properties-link" href="">All Properties</a> /
            <a class="saga-properties-link saga-properties-link--active" href="">Updated Properties</a>
          </div>

          <!-- Display saga properties if available -->
          <ul class="saga-properties-list">
            <li class="saga-properties-list-item">
              <span class="saga-properties-list-text" title="Property (new)">Property (new)</span>
              <span class="saga-properties-list-text">=</span>
              <span class="saga-properties-list-text" title="Sample Value"> Sample Value</span>
            </li>
          </ul>
        </div>
      </div>

      <!-- Right side - outgoing messages (non-timeout) -->
      <div class="cell cell--side cell--aling-top" v-if="update.HasOutgoingMessages">
        <div class="cell-inner cell-inner-right"></div>
        <SagaOutgoingMessage v-for="(msg, msgIndex) in update.OutgoingMessages" :key="msgIndex" :message="msg" :showMessageData="showMessageData" />
      </div>
    </div>

    <!-- Display each outgoing timeout message in separate rows -->
    <SagaOutgoingTimeoutMessage v-for="(msg, msgIndex) in update.OutgoingTimeoutMessages" :key="'timeout-' + msgIndex" :message="msg" :isLastMessage="msgIndex === update.OutgoingTimeoutMessages.length - 1" :showMessageData="showMessageData" />
  </div>
</template>

<style scoped>
.block {
  /* block container style */
}

.row {
  display: flex;
}

.row--right {
  justify-content: right;
}

.cell {
  padding: 0;
}

.cell-flex {
  display: flex;
}

.cell--side {
  align-self: flex-end;
  width: 25%;
  padding: 0;
}

.cell--aling-top {
  align-self: flex-start;
}

.cell--left-border {
  border-top: solid 2px #000000;
}

.cell--center {
  width: 50%;
  background-color: #f2f2f2;
  border: 0;
}

.cell--center--border {
  display: flex;
  flex-direction: column;
  border-top: solid 2px #000000;
}

.cell-inner {
  /* padding: 0.5rem; */
}

.cell-inner-center {
  padding: 0.5rem;
}

.cell-inner-center:first-child {
  flex-grow: 1;
}

.cell-inner-line {
  flex-grow: 1;
  padding: 0.25rem 0.5rem;
  border-left: solid 2px #000000;
  margin-left: 1rem;
}

.cell-inner-line:first-child {
  flex-grow: 1;
}

.cell-inner-side {
  margin-top: 1rem;
  padding: 0.25rem 0.25rem 0;
  border: solid 2px #cccccc;
  background-color: #cccccc;
}

.cell-inner-side:nth-child(-n + 2) {
  margin-top: 0;
}

.cell-inner-side--active {
  border: solid 2px #000000;
}

.cell-inner-right {
  position: relative;
  min-height: 2.5rem;
  border: solid 2px #000000;
  border-left: 0;
  border-bottom: 0;
  margin-right: 50%;
}

.cell-inner-right:after {
  position: absolute;
  display: block;
  content: "";
  border: solid 6px #000000;
  border-top-width: 10px;
  border-left-color: transparent;
  border-right-color: transparent;
  border-bottom: 0;
  bottom: 0;
  margin-left: 100%;
  left: -5px;
}

.cell-inner--align-bottom {
  align-self: flex-end;
}

.message-title {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 900;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.saga-status-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 900;
}

.saga-status-title--inline {
  display: inline-block;
}

.timestamp {
  font-size: 0.9rem;
}

.timestamp--inline {
  display: inline-block;
  margin-left: 0.5rem;
  font-size: 0.8rem;
}

.saga-properties {
  margin: 0 -0.25rem;
  padding: 0.25rem;
  font-size: 0.6rem;
  text-transform: uppercase;
}

.saga-properties-link {
  padding: 0 0.25rem;
  text-decoration: underline;
}

.saga-properties-link--active {
  font-weight: 900;
  color: #000000;
}

.saga-properties-list {
  margin: 0;
  padding-left: 0.25rem;
  list-style: none;
}

.saga-properties-list-item {
  display: flex;
}

.saga-properties-list-text {
  display: inline-block;
  padding-top: 0.25rem;
  padding-right: 0.75rem;
  overflow: hidden;
  font-size: 0.75rem;
  white-space: nowrap;
}

.saga-properties-list-text:first-child {
  min-width: 8rem;
  max-width: 8rem;
  display: inline-block;
  text-overflow: ellipsis;
}

.saga-properties-list-text:last-child {
  padding-right: 0;
  text-overflow: ellipsis;
}

.message-data {
  display: none;
  padding: 0.2rem;
  background-color: #ffffff;
  border: solid 1px #cccccc;
  font-size: 0.75rem;
}

.message-data--active {
  display: block;
}

.saga-icon {
  display: block;
  float: left;
  margin-right: 0.35rem;
}

.saga-icon--side-cell {
  width: 2rem;
  height: 2rem;
  padding: 0.23rem;
}

.saga-icon--center-cell {
  float: none;
  display: inline;
  width: 1rem;
  height: 1rem;
  margin-top: -0.3rem;
}
.timeout-status {
  display: inline-block;
  font-size: 1rem;
  font-weight: 900;
  color: #00a3c4;
}
</style>
