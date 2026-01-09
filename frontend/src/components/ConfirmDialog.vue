<script setup lang="ts">
import ActionButton from "@/components/ActionButton.vue";
import { onMounted, onUnmounted } from "vue";

const emit = defineEmits<{ confirm: []; cancel: [] }>();

withDefaults(
  defineProps<{
    heading: string;
    body: string;
    secondParagraph?: string;
    hideCancel?: boolean;
  }>(),
  { hideCancel: false, secondParagraph: "" }
);

function confirm() {
  emit("confirm");
}

function close() {
  emit("cancel");
}

onUnmounted(() => {
  // Must remove the class again once the modal is dismissed
  document.getElementsByTagName("body")[0].className = "";
});

onMounted(() => {
  // Add the `modal-open` class to the body tag
  document.getElementsByTagName("body")[0].className = "modal-open";
});
</script>

<template>
  <div class="modal-mask">
    <div class="modal-wrapper">
      <div class="modal-container" role="dialog" :aria-label="heading">
        <div class="modal-header">
          <div class="modal-title">
            <h3>{{ heading }}</h3>
          </div>
        </div>
        <div class="modal-body">
          <p>{{ body }}</p>
          <p v-if="secondParagraph && secondParagraph.length">{{ secondParagraph }}</p>
        </div>
        <div class="modal-footer">
          <ActionButton variant="primary" :aria-label="hideCancel ? 'Ok' : 'Yes'" @click="confirm">{{ hideCancel ? "Ok" : "Yes" }}</ActionButton>
          <ActionButton v-if="!hideCancel" aria-label="No" @click="close">No</ActionButton>
        </div>
      </div>
    </div>
  </div>
</template>
<style scoped>
@import "@/components/modal.css";
</style>
