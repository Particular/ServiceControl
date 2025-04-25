<script setup lang="ts">
import { computed, onUnmounted, watch } from "vue";
import routeLinks from "@/router/routeLinks";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { useMessageStore } from "@/stores/MessageStore";
import { storeToRefs } from "pinia";
import ToolbarEndpointIcon from "@/assets/Shell_ToolbarEndpoint.svg";
import { SagaViewModel, parseSagaUpdates } from "./SagaDiagram/SagaDiagramParser";
import { typeToName } from "@/composables/typeHumanizer";
import LoadingSpinner from "@/components/LoadingSpinner.vue";

//Subcomponents
import NoSagaData from "./SagaDiagram/NoSagaData.vue";
import SagaPluginNeeded from "./SagaDiagram/SagaPluginNeeded.vue";
import SagaHeader from "./SagaDiagram/SagaHeader.vue";
import SagaUpdateNode from "./SagaDiagram/SagaUpdateNode.vue";
import SagaCompletedNode from "./SagaDiagram/SagaCompletedNode.vue";

const sagaDiagramStore = useSagaDiagramStore();
const { showMessageData, loading } = storeToRefs(sagaDiagramStore);

const messageStore = useMessageStore();

watch(
  () => messageStore.state.data.invoked_saga?.has_saga,
  (hasSaga) => {
    const saga = messageStore.state.data.invoked_saga;
    if (hasSaga && saga?.saga_id) {
      sagaDiagramStore.setSagaId(saga.saga_id);
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

  const { data } = messageStore.state;
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
    SagaUpdates: parseSagaUpdates(sagaHistory, sagaDiagramStore.messagesData),
    ShowMessageData: showMessageData.value,
  };
});
</script>

<template>
  <div class="saga-container">
    <!-- Toolbar header -->
    <div v-if="vm.HasSagaData" class="header">
      <button :class="['saga-button', { 'saga-button--active': vm.ShowMessageData }]" aria-label="show-message-data-button" @click="sagaDiagramStore.toggleMessageData">
        <img class="saga-button-icon" :src="ToolbarEndpointIcon" alt="" />
        {{ vm.ShowMessageData ? "Hide Message Data" : "Show Message Data" }}
      </button>
    </div>

    <!-- Loading Spinner -->
    <div v-if="loading" class="loading-container">
      <LoadingSpinner />
    </div>

    <!-- No saga Data Available container -->
    <NoSagaData v-else-if="!vm.ParticipatedInSaga" />

    <!-- Saga Audit Plugin Needed container -->
    <SagaPluginNeeded v-else-if="vm.ShowNoPluginActiveLegend" />

    <!-- Main Saga Data container -->
    <div v-else-if="vm.HasSagaData" role="table" aria-label="saga-sequence-list" class="body" style="display: flex">
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

.loading-container {
  display: flex;
  flex: 1;
  justify-content: center;
  align-items: center;
  min-height: 200px;
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
