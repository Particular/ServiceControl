<script setup lang="ts">
import ConfirmDialog from "@/components/ConfirmDialog.vue";
import { useMessageViewStore } from "@/stores/MessageViewStore.ts";
import { computed, ref } from "vue";
import { showToastAfterOperation } from "@/composables/toast.ts";
import { TYPE } from "vue-toastification";

const store = useMessageViewStore();
const isConfirmDialogVisible = ref(false);

const failureStatus = computed(() => store.state.data.failure_status);

const handleConfirm = async () => {
  isConfirmDialogVisible.value = false;

  const message = `Restoring the message ${store.state.data.id} ...`;
  await showToastAfterOperation(store.restoreMessage, TYPE.INFO, "Info", message);
  store.state.data.failure_status.restoring = true;
};
</script>

<template>
  <template v-if="failureStatus.archived">
    <button type="button" class="btn btn-default" @click="isConfirmDialogVisible = true"><i class="fa fa-undo"></i> Restore</button>
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
