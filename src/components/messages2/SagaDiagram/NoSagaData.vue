<script setup lang="ts">
import NoSagaIcon from "@/assets/NoSaga.svg";
</script>

<template>
  <div class="body">
    <div class="saga-message">
      <div class="saga-message-container">
        <img class="saga-message-image" :src="NoSagaIcon" alt="" v-tippy="`No saga data found`" />
        <h1 role="status" aria-label="message-not-involved-in-saga" class="saga-message-title" v-tippy="`This message is not part of any saga`">No Saga Data Available</h1>
      </div>
    </div>
  </div>
</template>

<style scoped>
.body {
  display: flex;
  flex: 1;
  justify-content: center;
}

.saga-message {
  display: flex;
  align-items: center;
}

.saga-message-image {
  display: block;
  margin: auto;
}

.saga-message-title {
  text-align: center;
  margin: 0.2rem 0 0;
  font-size: 1.5rem;
  color: #cccccc;
}
</style>
