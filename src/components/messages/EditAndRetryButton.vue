<script setup lang="ts">
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import EditRetryDialog2 from "@/components/failedmessages/EditRetryDialog2.vue";
import { MessageStatus } from "@/resources/Message.ts";

const store = useMessageViewStore();
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => store.state.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.archived || failureStatus.value.resolved);
const isVisible = computed(() => store.edit_and_retry_config.enabled && store.state.data.status !== MessageStatus.Successful && store.state.data.status !== MessageStatus.ResolvedSuccessfully);
const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Retrying the edited message ${store.state.data.id} ...`;
  await showToastAfterOperation(store.retryMessage, TYPE.INFO, "Info", message);
  store.state.data.failure_status.retried = true;
};
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" aria-label="Edit & retry" :disabled="isDisabled" @click="isConfirmDialogVisible = true"><i class="fa fa-pencil"></i> Edit & retry</button>
    <Teleport to="#modalDisplay">
      <EditRetryDialog2 v-if="isConfirmDialogVisible" @cancel="isConfirmDialogVisible = false" @confirm="handleConfirm"></EditRetryDialog2>
    </Teleport>
  </template>
</template>
