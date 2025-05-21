<script setup lang="ts">
import { SagaTimeoutMessageViewModel } from "./SagaDiagramParser";
import MessageDataBox from "./MessageDataBox.vue";
import TimeoutIcon from "@/assets/TimeoutIcon.svg";
import SagaTimeoutIcon from "@/assets/SagaTimeoutIcon.svg";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { computed, ref, watch } from "vue";

const props = defineProps<{
  message: SagaTimeoutMessageViewModel;
  isLastMessage: boolean;
  showMessageData?: boolean;
}>();

const store = useSagaDiagramStore();
const timeoutMessageRef = ref<HTMLElement | null>(null);
const shouldBeActive = computed(() => {
  return store.selectedMessageId === props.message.MessageId;
});

// This sets the store with the required values so the timeout invocation node exists, it will react by scrolling to the node
const navigateToTimeout = () => {
  // Set the selected message ID in the store
  store.setSelectedMessageId(props.message.MessageId);
  store.scrollToTimeout = true;
};

watch(
  [() => store.scrollToTimeoutRequest, () => shouldBeActive.value, () => timeoutMessageRef.value !== null],
  ([scrollRequest, shouldScroll, refExists]) => {
    if (scrollRequest && shouldScroll && refExists && timeoutMessageRef.value) {
      timeoutMessageRef.value.scrollIntoView({
        behavior: "smooth",
        block: "center",
      });

      store.scrollToTimeoutRequest = false;
    }
  },
  { immediate: true }
);
</script>

<template>
  <div class="row row--right">
    <div class="cell cell--center">
      <div class="cell-inner cell-inner-line">
        <img class="saga-icon saga-icon--center-cell saga-icon--overlap" :src="SagaTimeoutIcon" alt="Timeout Request" />
        <a v-if="message.HasBeenProcessed" v-tippy="`View timeout processing details`" class="timeout-status" href="#" @click.prevent="navigateToTimeout" aria-label="timeout requested">Timeout Requested = {{ message.TimeoutFriendly }}</a>
        <span v-else class="timeout-status" aria-label="timeout requested" v-tippy="`This timeout has been requested but not yet processed`">Timeout Requested = {{ message.TimeoutFriendly }}</span>
      </div>
    </div>
    <div class="cell cell--side"></div>
    <div class="cell cell--center cell--top-border">
      <div class="cell-inner cell-inner-top"></div>
      <div v-if="!isLastMessage" class="cell-inner cell-inner-line"></div>
    </div>
    <div class="cell cell--side">
      <div class="cell-inner cell-inner-right"></div>
      <div
        ref="timeoutMessageRef"
        :class="{
          'cell-inner': true,
          'cell-inner-side': true,
          'cell-inner-side--active': shouldBeActive,
        }"
      >
        <img class="saga-icon saga-icon--side-cell" :src="TimeoutIcon" alt="" v-tippy="`Timeout Message`" />
        <h2 class="message-title" aria-label="timeout message type" v-tippy="message.FriendlyTypeName">{{ message.FriendlyTypeName }}</h2>
        <div class="timestamp" aria-label="timeout message timestamp" v-tippy="`Sent at: ${message.FormattedTimeSent}`">{{ message.FormattedTimeSent }}</div>
      </div>
      <div v-if="showMessageData" class="message-data message-data--active">
        <MessageDataBox :messageData="message.Data" :maximizedTitle="message.FriendlyTypeName" />
      </div>
    </div>
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

.cell-inner-side {
  padding: 0.25rem 0.25rem 0;
  border: solid 2px #cccccc;
  background-color: #cccccc;
}

.cell-inner-side--active {
  border: solid 5px #0b6eef;
  animation: blink-border 1.8s ease-in-out;
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

.saga-icon--overlap {
  margin-left: -1rem;
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
