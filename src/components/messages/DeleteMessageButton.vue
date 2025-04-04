<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";
import { MessageStatus } from "@/resources/Message.ts";
import { storeToRefs } from "pinia";
import { FailedMessageStatus } from "@/resources/FailedMessage.ts";

const store = useMessageViewStore();
const { state } = storeToRefs(store);
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => state.value.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.resolved);
const isVisible = computed(() => !failureStatus.value.archived && state.value.data.status !== MessageStatus.Successful && state.value.data.status !== MessageStatus.ResolvedSuccessfully);

const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Deleting the message ${state.value.data.id} ...`;
  await showToastAfterOperation(store.archiveMessage, TYPE.INFO, "Info", message);

  await store.pollForNextUpdate(FailedMessageStatus.Archived);
};
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" :disabled="isDisabled" @click="isConfirmDialogVisible = true"><i class="fa fa-trash"></i> Delete message</button>
    <Teleport to="#modalDisplay">
      <ConfirmDialog
        v-if="isConfirmDialogVisible"
        @cancel="isConfirmDialogVisible = false"
        @confirm="handleConfirm"
        heading="Are you sure you want to delete this message?"
        body="If you delete, this message won't be available for retrying unless it is later restored."
      /> </Teleport
  ></template>
</template>
