<script setup lang="ts">
import ActionButton from "@/components/ActionButton.vue";
import { useMessageStore } from "@/stores/MessageStore";
import { useDownloadFileFromString } from "@/composables/fileDownloadCreator";
import { showToastAfterOperation } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { ref } from "vue";
import { faDownload } from "@fortawesome/free-solid-svg-icons";

const store = useMessageStore();
const executing = ref(false);

async function exportMessage() {
  executing.value = true;
  await showToastAfterOperation(
    async () => {
      const exportedString = await store.exportMessage();
      useDownloadFileFromString(exportedString, "text/txt", "message.txt");
    },
    TYPE.INFO,
    "Info",
    "Message export completed."
  );
  executing.value = false;
}
</script>

<template>
  <ActionButton :icon="faDownload" :loading="executing" @click="exportMessage">Export message</ActionButton>
</template>
