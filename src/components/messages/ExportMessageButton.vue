<script setup lang="ts">
import { useMessageStore } from "@/stores/MessageStore";
import { useDownloadFileFromString } from "@/composables/fileDownloadCreator";
import { showToastAfterOperation } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { ref } from "vue";

const store = useMessageStore();
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
