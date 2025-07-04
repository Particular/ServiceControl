<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageStore } from "@/stores/MessageStore";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { MessageStatus } from "@/resources/Message";
import { storeToRefs } from "pinia";
import { FailedMessageStatus } from "@/resources/FailedMessage";
import FAIcon from "@/components/FAIcon.vue";
import { faTrash } from "@fortawesome/free-solid-svg-icons";

const store = useMessageStore();
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
    <button type="button" class="btn btn-default" :disabled="isDisabled" @click="isConfirmDialogVisible = true"><FAIcon :icon="faTrash" class="icon" /> Delete message</button>
    <Teleport to="#modalDisplay">
      <ConfirmDialog
        v-if="isConfirmDialogVisible"
        @cancel="isConfirmDialogVisible = false"
        @confirm="handleConfirm"
        heading="Are you sure you want to delete this message?"
        body="If it is deleted, this message won't be available for retrying unless it is later restored."
      />
    </Teleport>
  </template>
</template>

<style scoped>
.icon {
  color: var(--reduced-emphasis);
}
</style>
