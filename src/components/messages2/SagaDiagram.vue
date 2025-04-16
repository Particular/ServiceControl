<script setup lang="ts">
import { computed, onUnmounted, watch, ref } from "vue";
import routeLinks from "@/router/routeLinks";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { useMessageStore } from "@/stores/MessageStore";
import { storeToRefs } from "pinia";
import ToolbarEndpointIcon from "@/assets/Shell_ToolbarEndpoint.svg";
import { SagaViewModel, parseSagaUpdates } from "./SagaDiagram/useSagaDiagramParser";
import { typeToName } from "@/composables/typeHumanizer";

//Subcomponents
import NoSagaData from "./SagaDiagram/NoSagaData.vue";
import SagaPluginNeeded from "./SagaDiagram/SagaPluginNeeded.vue";
import SagaHeader from "./SagaDiagram/SagaHeader.vue";
import SagaUpdateNode from "./SagaDiagram/SagaUpdateNode.vue";
import SagaCompletedNode from "./SagaDiagram/SagaCompletedNode.vue";

const showMessageData = ref(false);

const toggleMessageData = () => {
  showMessageData.value = !showMessageData.value;
};

const store = useMessageStore();
const { state: messageState } = storeToRefs(store);

const sagaDiagramStore = useSagaDiagramStore();

//Watch for message and set saga ID when component mounts or message changes
watch(
  () => messageState.value.data.invoked_saga,
  (newSagas) => {
    if (newSagas.has_saga) {
      sagaDiagramStore.setSagaId(newSagas.saga_id || "");
    } else {
      sagaDiagramStore.clearSagaHistory();
    }
  },
  { immediate: true }
);

onUnmounted(() => {
  sagaDiagramStore.clearSagaHistory();
});

const vm = computed<SagaViewModel>(() => {
  const completedUpdate = sagaDiagramStore.sagaHistory?.changes.find((update) => update.status === "completed");
  const completionTime = completedUpdate ? new Date(completedUpdate.finish_time) : null;

  const { data } = messageState.value;
  const { invoked_saga: saga } = data;
  const sagaHistory = sagaDiagramStore.sagaHistory;

  return {
    // Saga metadata
    SagaTitle: typeToName(saga.saga_type) || "Unknown saga",
    SagaGuid: saga.saga_id || "Missing guid",

    // Navigation
    MessageIdUrl: routeLinks.messages.successMessage.link(data.message_id || "", data.id || ""),

    // Status flags
    ParticipatedInSaga: saga.has_saga || false,
    HasSagaData: !!sagaHistory,
    ShowNoPluginActiveLegend: (!sagaHistory && saga.has_saga) || false,
    SagaCompleted: !!completedUpdate,

    // Display data
    FormattedCompletionTime: completionTime ? `${completionTime.toLocaleDateString()} ${completionTime.toLocaleTimeString()}` : "",
    SagaUpdates: parseSagaUpdates(sagaHistory),
    ShowMessageData: showMessageData.value,
  };
});
</script>

<template>
  <div class="saga-container">
    <!-- Toolbar header -->
    <div v-if="vm.HasSagaData" class="header">
      <button :class="['saga-button', { 'saga-button--active': vm.ShowMessageData }]" aria-label="show-message-data-button" @click="toggleMessageData">
        <img class="saga-button-icon" :src="ToolbarEndpointIcon" alt="" />
        {{ vm.ShowMessageData ? "Hide Message Data" : "Show Message Data" }}
      </button>
    </div>

    <!-- No saga Data Available container -->
    <NoSagaData v-if="!vm.ParticipatedInSaga" />

    <!-- Saga Audit Plugin Needed container -->
    <SagaPluginNeeded v-if="vm.ShowNoPluginActiveLegend" />

    <!-- Main Saga Data container -->
    <div v-if="vm.HasSagaData" role="table" aria-label="saga-sequence-list" class="body" style="display: flex">
      <div class="container">
        <!-- Saga header with title and navigation -->
        <SagaHeader :saga-title="vm.SagaTitle" :saga-guid="vm.SagaGuid" :message-id-url="vm.MessageIdUrl" />

        <!-- Iterate through each saga update -->
        <SagaUpdateNode v-for="(update, index) in vm.SagaUpdates" :key="index" :update="update" :show-message-data="vm.ShowMessageData" />

        <!-- Saga Completed section -->
        <SagaCompletedNode v-if="vm.SagaCompleted" :completion-time="vm.FormattedCompletionTime" />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Layout styles */

.saga-container {
  display: flex;
  flex-direction: column;
  /* Must validate parent height in order to set this element min-height value */
  min-height: 500px;
  background-color: #ffffff;
}

/* Main containers */

.header {
  padding: 0.5rem;
  border-bottom: solid 2px #ddd;
}
.body {
  display: flex;
  flex: 1;
  justify-content: center;
}

.container {
  width: 66.6667%;
  min-width: 50rem;
}

/* Button styles */

.saga-button {
  display: block;
  padding: 0.2rem 0.7rem 0.1rem;
  color: #555555;
  font-size: 0.75rem;
  border: solid 2px #00a3c4;
  background-color: #e3e4e5;
}
.saga-button:focus,
.saga-button:hover {
  background-color: #daebfc;
}

.saga-button:active,
.saga-button--active {
  background-color: #c3dffc;
}
.saga-button-icon {
  width: 0.75rem;
  height: 0.75rem;
  margin-top: -0.2rem;
  margin-right: 0.25rem;
}
</style>
