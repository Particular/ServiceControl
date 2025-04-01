<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import { MessageStatus } from "@/resources/Message.ts";

const store = useMessageViewStore();
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => store.state.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.archived || failureStatus.value.resolved);
const isVisible = computed(() => store.edit_and_retry_config.enabled && store.state.data.status !== MessageStatus.Successful && store.state.data.status !== MessageStatus.ResolvedSuccessfully);

const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Retrying the message ${store.state.data.id} ...`;
  await showToastAfterOperation(store.retryMessage, TYPE.INFO, "Info", message);
  store.state.data.failure_status.retried = true;
};
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" :disabled="isDisabled" @click="isConfirmDialogVisible = true"><i class="fa fa-refresh"></i> Retry message</button>
    <Teleport to="#modalDisplay">
      <ConfirmDialog v-if="isConfirmDialogVisible" @cancel="isConfirmDialogVisible = false" @confirm="handleConfirm" heading="Retry Message" body="Are you sure you want to retry this message?" />
    </Teleport>
  </template>
</template>
