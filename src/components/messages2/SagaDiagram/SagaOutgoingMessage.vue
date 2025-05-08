<script setup lang="ts">
import MessageDataBox from "./MessageDataBox.vue";
import CommandIcon from "@/assets/command.svg";
import EventIcon from "@/assets/event.svg";
import { SagaMessageViewModel } from "./SagaDiagramParser";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { computed } from "vue";

const shouldBeActive = computed(() => {
  return store.selectedMessageId === props.message.MessageId;
});

const store = useSagaDiagramStore();

const props = defineProps<{
  message: SagaMessageViewModel;
  showMessageData?: boolean;
}>();
</script>

<template>
  <div
    :class="{
      'cell-inner': true,
      'cell-inner-side': true,
      'cell-inner-side--active': shouldBeActive,
    }"
  >
    <img class="saga-icon saga-icon--side-cell" :src="message.IsEventMessage ? EventIcon : CommandIcon" :alt="message.IsEventMessage ? 'Event' : 'Command'" />
    <h2 class="message-title">{{ message.FriendlyTypeName }}</h2>
    <div class="timestamp">{{ message.FormattedTimeSent }}</div>
  </div>
  <div v-if="showMessageData" class="message-data message-data--active">
    <MessageDataBox :messageData="message.Data" :maximizedTitle="message.FriendlyTypeName" />
  </div>
</template>

<style scoped>
.row {
  display: flex;
}

.row--right {
  justify-content: right;
}

.cell {
  padding: 0;
}

.cell--center {
  width: 50%;
  background-color: #f2f2f2;
  border: 0;
}

.cell--side {
  align-self: flex-end;
  width: 25%;
  padding: 0;
}

.cell--top-border {
  display: flex;
  flex-direction: column;
}

.cell-inner-top {
  border-top: solid 2px #000000;
  margin-left: 1rem;
}

.cell-inner-line {
  flex-grow: 1;
  padding: 0.25rem 0.5rem;
  border-left: solid 2px #000000;
  margin-left: 1rem;
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

.message-title {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 900;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.timestamp {
  font-size: 0.9rem;
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

.timeout-status {
  display: inline-block;
  font-size: 1rem;
  font-weight: 900;
}

.saga-icon--center-cell {
  float: none;
  display: inline;
  width: 1rem;
  height: 1rem;
  margin-top: -0.3rem;
}

.saga-icon--overlap {
  margin-left: -1rem;
}

.cell-inner-side:nth-child(-n + 2) {
  margin-top: 0;
}

.cell-inner-side {
  margin-top: 1rem;
  padding: 0.25rem 0.25rem 0;
  border: solid 2px #cccccc;
  background-color: #cccccc;
}

.cell-inner-side--active {
  border: solid 5px #0b6eef;
  animation: blink-border 1.8s ease-in-out;
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
@keyframes blink-border {
  0%,
  100% {
    border-color: #0b6eef;
  }
  20%,
  60% {
    border-color: #cccccc;
  }
  40%,
  80% {
    border-color: #0b6eef;
  }
}
</style>
