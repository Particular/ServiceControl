<script setup lang="ts">
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import EditRetryDialog2 from "@/components/failedmessages/EditRetryDialog2.vue";
import { MessageStatus } from "@/resources/Message.ts";
import { storeToRefs } from "pinia";
import { FailedMessageStatus } from "@/resources/FailedMessage.ts";

const store = useMessageViewStore();
const { state } = storeToRefs(store);
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => state.value.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.archived || failureStatus.value.resolved);
const isVisible = computed(() => store.edit_and_retry_config.enabled && state.value.data.status !== MessageStatus.Successful && state.value.data.status !== MessageStatus.ResolvedSuccessfully);
const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Retrying the edited message ${state.value.data.id} ...`;
  await showToastAfterOperation(store.retryMessage, TYPE.INFO, "Info", message);

  await store.pollForNextUpdate(FailedMessageStatus.Resolved);
};

async function openDialog() {
  await store.downloadBody();
  isConfirmDialogVisible.value = true;
}
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" aria-label="Edit & retry" :disabled="isDisabled" @click="openDialog"><i class="fa fa-pencil"></i> Edit & retry</button>
    <Teleport to="#modalDisplay">
      <EditRetryDialog2 v-if="isConfirmDialogVisible" @cancel="isConfirmDialogVisible = false" @confirm="handleConfirm"></EditRetryDialog2>
    </Teleport>
  </template>
</template>
