<script setup lang="ts">
import NoSagaIcon from "@/assets/NoSaga.svg";
import CopyToClipboard from "@/components/CopyToClipboard.vue";
</script>

<template>
  <div class="body" role="status" aria-label="saga-plugin-needed">
    <div class="saga-message">
      <div class="saga-message-container">
        <img class="saga-message-image" :src="NoSagaIcon" alt="" v-tippy="`Saga plugin required`" />
        <h1 class="saga-message-title" v-tippy="`The saga audit plugin is required to show saga visualization`">Saga audit plugin needed to visualize saga</h1>
        <div class="saga-message-box">
          <p class="saga-message-text" v-tippy="`You need to install the NServiceBus.SagaAudit package in your endpoint`">To visualize your saga, please install the appropriate nuget package in your endpoint</p>
          <a href="https://www.nuget.org/packages/NServiceBus.SagaAudit" class="saga-message-link" v-tippy="`Go to NServiceBus.SagaAudit NuGet page`">install-package NServiceBus.SagaAudit</a>
          <span class="clippy-button"><CopyToClipboard value="install-package NServiceBus.SagaAudit" :isIconOnly="true" /></span>
        </div>
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

.saga-message-box {
  margin-top: 1rem;
  padding: 1.5rem 2rem;
  color: #535353;
  font-size: 1rem;
  font-weight: 900;
  text-align: center;
  background-color: #e4e4e4;
}

.saga-message-text {
  margin: 0;
}

.saga-message-link {
  font-family: "Courier New", Courier, monospace;
  color: #aaaaaa;
}

.clippy-button {
  white-space: pre;
}

.clippy-button::before {
  content: "\00a0";
}
</style>
