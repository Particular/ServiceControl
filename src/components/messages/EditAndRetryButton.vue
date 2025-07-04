<script setup lang="ts">
import { useMessageStore } from "@/stores/MessageStore";
import { computed, ref } from "vue";
import { useShowToast } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import EditRetryDialog from "@/components/failedmessages/EditRetryDialog.vue";
import { MessageStatus } from "@/resources/Message";
import { storeToRefs } from "pinia";
import { FailedMessageStatus } from "@/resources/FailedMessage";
import FAIcon from "@/components/FAIcon.vue";
import { faPencil } from "@fortawesome/free-solid-svg-icons";

const store = useMessageStore();
const { state, edit_and_retry_config } = storeToRefs(store);
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => state.value.data.failure_status);
const isDisabled = computed(() => failureStatus.value.retried || failureStatus.value.archived || failureStatus.value.resolved);
const isVisible = computed(() => edit_and_retry_config.value.enabled && state.value.data.status !== MessageStatus.Successful && state.value.data.status !== MessageStatus.ResolvedSuccessfully);
const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Retrying the edited message ${state.value.data.id} ...`;
  useShowToast(TYPE.INFO, "Info", message);
  await store.pollForNextUpdate(FailedMessageStatus.Resolved);
};

async function openDialog() {
  await store.downloadBody();
  isConfirmDialogVisible.value = true;
}
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" aria-label="Edit & retry" :disabled="isDisabled" @click="openDialog"><FAIcon :icon="faPencil" class="icon" /> Edit & retry</button>
    <Teleport to="#modalDisplay">
      <EditRetryDialog v-if="isConfirmDialogVisible" @cancel="isConfirmDialogVisible = false" @confirm="handleConfirm"></EditRetryDialog>
    </Teleport>
  </template>
</template>

<style scoped>
.icon {
  color: var(--reduced-emphasis);
}
</style>
