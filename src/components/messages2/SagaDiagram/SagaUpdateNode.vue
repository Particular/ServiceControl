<script setup lang="ts">
import { SagaUpdateViewModel } from "./SagaDiagramParser";
import MessageDataBox from "./MessageDataBox.vue";
import SagaOutgoingTimeoutMessage from "./SagaOutgoingTimeoutMessage.vue";
import SagaOutgoingMessage from "./SagaOutgoingMessage.vue";
import DiffViewer from "@/components/messages2/DiffViewer.vue";
import MaximizableCodeEditor from "@/components/MaximizableCodeEditor.vue";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { ref, watch, computed } from "vue";
import { EditorView } from "@codemirror/view";
import { parse, stringify } from "lossless-json";

// Import the images directly
import CommandIcon from "@/assets/command.svg";
import SagaInitiatedIcon from "@/assets/SagaInitiatedIcon.svg";
import SagaUpdatedIcon from "@/assets/SagaUpdatedIcon.svg";
import TimeoutIcon from "@/assets/timeout.svg";
import EventIcon from "@/assets/event.svg";
import SagaTimeoutIcon from "@/assets/SagaTimeoutIcon.svg";

// Define monospace theme with specific selectors for this component
const monospaceTheme = EditorView.baseTheme({
  ".maximazable-code-editor--inline-instance .cm-editor": {
    fontFamily: "monospace",
    fontSize: "0.75rem",
    backgroundColor: "#f2f2f2",
  },
  ".maximazable-code-editor--inline-instance .cm-scroller": {
    backgroundColor: "#f2f2f2",
  },
});

const props = defineProps<{
  update: SagaUpdateViewModel;
  showMessageData?: boolean;
}>();

const store = useSagaDiagramStore();
const initiatingMessageRef = ref<HTMLElement | null>(null);
const hasParsingError = ref(false);

const shouldBeActive = computed(() => {
  return store.selectedMessageId === props.update.MessageId;
});

const navigateToTimeoutRequest = () => {
  store.setSelectedMessageId(props.update.InitiatingMessage.MessageId);
  store.scrollToTimeoutRequest = true;
};

watch(
  [() => store.scrollToTimeout, () => shouldBeActive.value, () => initiatingMessageRef.value !== null],
  ([scrollTimeout, shouldScroll, refExists]) => {
    if (scrollTimeout && shouldScroll && refExists && initiatingMessageRef.value) {
      initiatingMessageRef.value.scrollIntoView({
        behavior: "smooth",
        block: "center",
      });

      store.scrollToTimeout = false;
    }
  },
  { immediate: true }
);

// Format a JSON value for display
const formatJsonValue = (value: unknown): string => {
  if (value === null || value === undefined) return "null";
  if (typeof value === "object") {
    return stringify(value as object, null, 2) || "{}";
  }
  return String(value);
};

// Process JSON state and remove standard properties
const processState = (state: string | undefined): object => {
  if (!state) return {};

  let stateObj: Record<string, unknown>;
  try {
    const parsedState = parse(state);

    stateObj = parsedState as Record<string, unknown>;
  } catch (e) {
    console.error("Error parsing state:", e);
    hasParsingError.value = true;
    return {};
  }

  // Filter out standard properties using delete
  const standardKeys = ["$type", "Id", "Originator", "OriginalMessageId"];
  standardKeys.forEach((key) => {
    if (key in stateObj) {
      delete stateObj[key];
    }
  });

  return stateObj;
};

const sagaUpdateStateChanges = computed(() => {
  const currentState = processState(props.update.stateAfterChange);
  const previousState = processState(props.update.previousStateAfterChange);
  const isFirstNode = props.update.IsFirstNode;

  // Format the current state
  const currentFormatted = formatJsonValue(currentState);

  // If it's the first node, just return the current state
  if (isFirstNode) {
    return {
      formattedState: currentFormatted,
      // Provide default empty strings for diff view to prevent type errors
      previousFormatted: "",
      currentFormatted: currentFormatted,
    };
  }

  // Format the JSON state objects
  const previousFormatted = formatJsonValue(previousState);

  return {
    previousFormatted,
    currentFormatted,
  };
});

// Determine if there are changes to display
const hasStateChanges = computed(() => {
  if (props.update.IsFirstNode) return true;

  const currentState = processState(props.update.stateAfterChange);
  const previousState = processState(props.update.previousStateAfterChange);

  return stringify(currentState) !== stringify(previousState);
});
</script>

<template>
  <div class="block" role="row">
    <!-- Initiating message and saga status header -->
    <div class="row">
      <div class="cell cell--side">
        <div
          ref="initiatingMessageRef"
          :class="{
            'cell-inner': true,
            'cell-inner-side': true,
            'cell-inner-side--active': shouldBeActive || (update.InitiatingMessage.IsSagaTimeoutMessage && update.MessageId === store.selectedMessageId),
          }"
          :data-message-id="update.InitiatingMessage.IsSagaTimeoutMessage ? update.MessageId : ''"
        >
          <img
            class="saga-icon saga-icon--side-cell"
            :src="update.InitiatingMessage.IsSagaTimeoutMessage ? TimeoutIcon : update.InitiatingMessage.IsEventMessage ? EventIcon : CommandIcon"
            alt=""
            v-tippy="update.InitiatingMessage.IsSagaTimeoutMessage ? `Timeout Message` : update.InitiatingMessage.IsEventMessage ? `Event Message` : `Command Message`"
          />
          <h2 class="message-title" aria-label="initiating message type" v-tippy="update.InitiatingMessage.FriendlyTypeName">{{ update.InitiatingMessage.FriendlyTypeName }}</h2>
          <div class="timestamp" aria-label="initiating message timestamp" v-tippy="`Received at: ${update.InitiatingMessage.FormattedMessageTimestamp}`">{{ update.InitiatingMessage.FormattedMessageTimestamp }}</div>
        </div>
      </div>
      <div class="cell cell--center cell-flex">
        <div class="cell-inner cell-inner-center cell-inner--align-bottom">
          <template v-if="update.InitiatingMessage.IsSagaTimeoutMessage">
            <img class="saga-icon saga-icon--center-cell" :src="SagaTimeoutIcon" alt="" v-tippy="`Saga Timeout`" />
            <a
              v-if="update.InitiatingMessage.HasRelatedTimeoutRequest"
              v-tippy="`View original timeout request`"
              href="#"
              @click.prevent="navigateToTimeoutRequest"
              class="saga-status-title saga-status-title--inline timeout-status"
              aria-label="timeout invoked"
            >
              Timeout Invoked
            </a>
            <h2 v-else class="saga-status-title saga-status-title--inline timeout-status" aria-label="timeout invoked">Timeout Invoked</h2>
            <br />
          </template>
          <img class="saga-icon saga-icon--center-cell" :src="update.IsFirstNode ? SagaInitiatedIcon : SagaUpdatedIcon" alt="" v-tippy="update.IsFirstNode ? `Saga Initiated` : `Saga Updated`" />
          <h2 class="saga-status-title saga-status-title--inline">
            {{ update.StatusDisplay }}
          </h2>
          <div class="timestamp timestamp--inline" aria-label="time stamp" v-tippy="`Update time: ${update.FormattedStartTime}`">
            {{ update.FormattedStartTime }}
          </div>
        </div>
      </div>
    </div>

    <!-- Saga properties and outgoing messages -->
    <div class="row">
      <!-- Left side - Message Data box -->
      <div class="cell cell--side cell--left-border cell--aling-top">
        <div v-if="showMessageData" class="message-data message-data--active">
          <!-- Generic message data box -->
          <MessageDataBox v-if="update.InitiatingMessage" :messageData="update.InitiatingMessage.MessageData" :maximizedTitle="update.InitiatingMessage.FriendlyTypeName" />
        </div>
      </div>

      <!-- Center - Saga properties -->
      <div class="cell cell--center cell--center--border">
        <div :class="{ 'cell-inner': true, 'cell-inner-line': update.HasTimeout, 'cell-inner-center': !update.HasTimeout }">
          <div class="saga-state-container">
            <h3 class="saga-state-title" v-if="update.IsFirstNode">Initial Saga State</h3>
            <h3 class="saga-state-title" v-else>State Changes</h3>

            <!-- Error message when parsing fails -->
            <div v-if="hasParsingError" class="json-container">
              <div class="parsing-error-message" v-tippy="`There was an error parsing the JSON data for the saga state`">An error occurred while parsing and displaying the saga state for this update</div>
            </div>

            <!-- Initial state display -->
            <div v-else-if="update.IsFirstNode" class="json-container json-container--first-node" v-tippy="`Initial state when saga was created`">
              <MaximizableCodeEditor :model-value="sagaUpdateStateChanges.formattedState || ''" language="json" :showGutter="false" modalTitle="Initial Saga State" :extensions="[monospaceTheme]" />
            </div>

            <!-- No changes message -->
            <div v-else-if="!hasStateChanges" class="json-container">
              <div class="no-changes-message" v-tippy="`This saga update didn't modify the saga's state data`">No state changes in this update</div>
            </div>

            <!-- Side-by-side diff view for state changes -->
            <div v-else-if="hasStateChanges && !update.IsFirstNode">
              <DiffViewer
                :hide-line-numbers="true"
                :showDiffOnly="true"
                :oldValue="sagaUpdateStateChanges.previousFormatted"
                :newValue="sagaUpdateStateChanges.currentFormatted"
                leftTitle="Previous State"
                rightTitle="Updated State"
                :showMaximizeIcon="true"
              />
            </div>
          </div>
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

.message-data {
  display: none;
  padding: 0.2rem;
  background-color: #ffffff;
  border: solid 1px #cccccc;
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
}

/* Styles for DiffViewer integration */
.saga-state-container {
  padding: 0.5rem;
}

.saga-state-title {
  margin: 0 0 0.5rem 0;
  font-size: 0.9rem;
  font-weight: bold;
}

.json-container {
  background-color: transparent;
}

.json-container--first-node {
  max-height: 300px;
  overflow: auto;
}

/* Override CodeEditor wrapper styles */
.json-container :deep(.wrapper.maximazable-code-editor--inline-instance) {
  border-radius: 0;
  border: none;
  background-color: #f2f2f2;
  margin-top: 0;
  font-size: 0.75rem;
}
.json-container :deep(.wrapper.maximazable-code-editor--inline-instance .toolbar) {
  border: none;
  border-radius: 0;
  background-color: transparent;
  padding: 0;
  margin-bottom: 0;
}

/* :deep(.maximazable-code-editor--inline-instance .cm-scroller) {
  background-color: #f2f2f2;
} */

.no-changes-message {
  padding: 1rem;
  text-align: center;
  font-style: italic;
  color: #666;
}

.parsing-error-message {
  padding: 1rem;
  text-align: center;
  font-style: italic;
  color: #a94442;
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
