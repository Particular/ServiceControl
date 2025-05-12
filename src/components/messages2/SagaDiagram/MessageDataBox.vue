<script setup lang="ts">
import { SagaMessageData, useSagaDiagramStore } from "@/stores/SagaDiagramStore";
import { storeToRefs } from "pinia";
import LoadingSpinner from "@/components/LoadingSpinner.vue";
import MaximizableCodeEditor from "@/components/MaximizableCodeEditor.vue";
import { computed } from "vue";
import parseContentType from "@/composables/contentTypeParser";

const props = defineProps<{
  messageData: SagaMessageData;
  maximizedTitle?: string;
}>();

const modalTitle = computed(() => {
  return props.maximizedTitle ? `Message Data - ${props.maximizedTitle}` : "Message Data";
});

const sagaDiagramStore = useSagaDiagramStore();
const { messageDataLoading } = storeToRefs(sagaDiagramStore);

const contentType = computed(() => parseContentType(props.messageData.body.data.content_type));

const body = computed(() => props.messageData.body.data.value || "");
</script>

<template>
  <div v-if="messageDataLoading" class="message-data-loading">
    <LoadingSpinner />
  </div>
  <div v-else-if="messageData.body.failed_to_load" class="message-data-box message-data-box-error">
    <span class="message-data-box-text--error" v-tippy="`Failed to load message data - there might be a connection issue or the message may no longer be available`">An error occurred while retrieving the message data</span>
  </div>
  <div v-else-if="!messageDataLoading && (!messageData.body.data.value || messageData.body.not_found)" class="message-data-box">
    <span class="message-data-box-text--empty" v-tippy="`No message body data available`">Empty</span>
  </div>
  <div v-else-if="contentType.isSupported" class="message-data-box message-data-box-content">
    <MaximizableCodeEditor :model-value="body" :language="contentType.language" :readOnly="true" :showGutter="false" :modalTitle="modalTitle" />
  </div>
  <div v-else class="message-data-box message-data-box-error">
    <span class="message-data-box-text--unsupported"
      >Message body cannot be displayed because content type "{{ messageData.body.data.content_type }}" is not supported.</span
    >
  </div>
</template>

<style scoped>
.message-data-box {
  display: flex;
}

.message-data-box-content {
  display: block;
}

.message-data-box-text {
  display: inline-block;
  margin-right: 0.25rem;
}

.message-data-box-text--ellipsis {
  display: inline-block;
  overflow: hidden;
  max-width: 100%;
  padding: 0%;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.message-data-box-text--empty {
  display: inline-block;
  width: 100%;
  text-align: center;
  color: #666;
  font-style: italic;
}

.message-data-box-text--error {
  display: inline-block;
  width: 100%;
  text-align: center;
  color: #a94442;
  font-style: italic;
}

.message-data-box-text--unsupported {
  display: inline-block;
  width: 100%;
  text-align: center;
  color: #8a6d3b;
  font-style: italic;
}

.message-data-loading {
  display: flex;
  justify-content: center;
  align-items: center;
}

.message-data-box-error {
  padding: 1rem;
  justify-content: center;
}
.message-data-box-content :deep(.wrapper.maximazable-code-editor--inline-instance) {
  border: none;
  border-radius: 0;
  margin-top: 0;
  font-size: 0.75rem;
}

.message-data-box-content :deep(.wrapper.maximazable-code-editor--inline-instance .toolbar) {
  border: none;
  border-radius: 0;
  background-color: transparent;
  padding: 0;
  margin-bottom: 0;
}

.message-data-box-content :deep(.wrapper.maximazable-code-editor--inline-instance .cm-editor) {
  /* Override any borders from the default theme */
  border: none;
}
</style>
