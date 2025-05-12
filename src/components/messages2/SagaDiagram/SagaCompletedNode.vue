<script setup lang="ts">
import SagaCompletedIcon from "@/assets/SagaCompletedIcon.svg";

defineProps<{
  completionTime: string;
}>();
</script>

<template>
  <div class="block">
    <div class="row row--center">
      <div class="cell cell--center cell--inverted">
        <div class="cell-inner cell-inner-center">
          <img class="saga-icon saga-icon--center-cell" :src="SagaCompletedIcon" alt="Saga Completed" />
          <h2 class="saga-status-title saga-status-title--inline" v-tippy="`The saga has completed its lifecycle`">Saga Completed</h2>
          <div class="timestamp" v-tippy="`Completion timestamp: ${completionTime}`">{{ completionTime }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.block {
  /* block container styles */
}

.row {
  display: flex;
}

.row--center {
  justify-content: center;
}

.cell--center {
  width: 50%;
  background-color: #f2f2f2;
  border: 0;
}

.cell--inverted {
  background-color: #333333;
  color: #ffffff;
  margin-bottom: 2rem;
}

.cell-inner-center {
  padding: 0.5rem;
}

.saga-status-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 900;
}

.saga-status-title--inline {
  display: inline-block;
}

.cell--inverted .saga-status-title {
  font-size: 0.9rem;
}

.timestamp {
  font-size: 0.9rem;
}

.cell--inverted .timestamp {
  margin-left: 1.2rem;
}

.saga-icon {
  display: block;
  float: left;
  margin-right: 0.35rem;
}

.saga-icon--center-cell {
  float: none;
  display: inline;
  width: 1rem;
  height: 1rem;
  margin-top: -0.3rem;
}
</style>
