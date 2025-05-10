<script setup lang="ts">
import { computed, onUnmounted, watch } from "vue";
import { useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { useMessageStore } from "@/stores/MessageStore";
import { storeToRefs } from "pinia";
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
    <div v-if="vm.HasSagaData" class="toolbar">
      <button type="button" class="btn btn-secondary btn-sm" v-tippy="`Toggle message data visibility`" aria-label="show-message-data-button" @click="sagaDiagramStore.toggleMessageData">
        <i class="fa fa-list-ul"></i> {{ vm.ShowMessageData ? "Hide Message Data" : "Show Message Data" }}
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
        <SagaHeader :saga-title="vm.SagaTitle" :saga-guid="vm.SagaGuid" />

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
  margin-top: 5px;
  border-radius: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ccc;
  background: white;
}

/* Main containers */

.toolbar {
  background-color: #f3f3f3;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
  margin-bottom: 0.5rem;
  display: flex;
  flex-direction: row;
  min-height: 40px;
}
.body {
  display: flex;
  flex: 1;
  justify-content: center;
}

.container {
  min-width: 50rem;
  max-width: 100rem;
}

.loading-container {
  display: flex;
  flex: 1;
  justify-content: center;
  align-items: center;
  min-height: 200px;
}
</style>
