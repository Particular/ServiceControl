<script setup lang="ts">
import { useMessageStore } from "@/stores/MessageStore";
import { useDownloadFileFromString } from "@/composables/fileDownloadCreator";
import { showToastAfterOperation } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { ref } from "vue";
import FAIcon from "@/components/FAIcon.vue";
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
  <button v-if="!executing" type="button" class="btn btn-default" @click="exportMessage"><FAIcon :icon="faDownload" class="icon" /> Export message</button>
  <button v-else type="button" class="btn btn-default" disabled>
    <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
    <span role="status"> Exporting message</span>
  </button>
</template>

<style scoped>
.icon {
  color: var(--reduced-emphasis);
}
</style>
