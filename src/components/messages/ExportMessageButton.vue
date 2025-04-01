<script setup lang="ts">
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { useDownloadFileFromString } from "@/composables/fileDownloadCreator.ts";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import { ref } from "vue";

const store = useMessageViewStore();
const executing = ref(false);

async function exportMessage() {
  executing.value = true;
  await showToastAfterOperation(
    async () => {
      const exportedString = await store.exportMessage();
      useDownloadFileFromString(exportedString, "text/txt", "failedMessage.txt");
    },
    TYPE.INFO,
    "Info",
    "Message export completed."
  );
  executing.value = false;
}
</script>

<template>
  <button v-if="!executing" type="button" class="btn btn-default" @click="exportMessage"><i class="fa fa-download"></i> Export message</button>
  <button v-else type="button" class="btn btn-default" disabled>
    <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
    <span role="status"> Exporting message</span>
  </button>
</template>

<style scoped></style>
