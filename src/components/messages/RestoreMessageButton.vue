<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageStore } from "@/stores/MessageStore";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast";
import { TYPE } from "vue-toastification";
import { storeToRefs } from "pinia";
import { FailedMessageStatus } from "@/resources/FailedMessage";
import FAIcon from "@/components/FAIcon.vue";
import { faUndo } from "@fortawesome/free-solid-svg-icons";

const store = useMessageStore();
const { state } = storeToRefs(store);
const isConfirmDialogVisible = ref(false);

const isVisible = computed(() => state.value.data.failure_status.archived);

const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Restoring the message ${state.value.data.id} ...`;
  await showToastAfterOperation(store.restoreMessage, TYPE.INFO, "Info", message);

  await store.pollForNextUpdate(FailedMessageStatus.Unresolved);
};
</script>

<template>
  <template v-if="isVisible">
    <button type="button" class="btn btn-default" @click="isConfirmDialogVisible = true"><FAIcon :icon="faUndo" class="icon" /> Restore</button>
    <Teleport to="#modalDisplay">
      <ConfirmDialog
        v-if="isConfirmDialogVisible"
        @cancel="isConfirmDialogVisible = false"
        @confirm="handleConfirm"
        heading="Are you sure you want to restore this message?"
        body="The restored message will be moved back to the list of failed messages."
      />
    </Teleport>
  </template>
</template>

<style scoped>
.icon {
  color: var(--reduced-emphasis);
}
</style>
