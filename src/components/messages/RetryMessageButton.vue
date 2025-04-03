<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import { MessageStatus } from "@/resources/Message.ts";
import { storeToRefs } from "pinia";

const store = useMessageViewStore();
const { state } = storeToRefs(store);
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => state.value.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.archived || failureStatus.value.resolved);
const isVisible = computed(() => store.edit_and_retry_config.enabled && state.value.data.status !== MessageStatus.Successful && state.value.data.status !== MessageStatus.ResolvedSuccessfully);

const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Retrying the message ${state.value.data.id} ...`;
  await showToastAfterOperation(store.retryMessage, TYPE.INFO, "Info", message);
  state.value.data.failure_status.retried = true;
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
