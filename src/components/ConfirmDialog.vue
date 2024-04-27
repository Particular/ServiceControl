<script setup lang="ts">
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
      <div class="modal-container">
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
          <button class="btn btn-primary" @click="confirm">{{ hideCancel ? "Ok" : "Yes" }}</button>
          <button v-if="!hideCancel" class="btn btn-default" @click="close">No</button>
        </div>
      </div>
    </div>
  </div>
</template>
<style>
.modal-mask {
  position: fixed;
  z-index: 9998;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(0, 0, 0, 0.5);
  display: table;
  transition: opacity 0.3s ease;
}

.modal-wrapper {
  display: table-cell;
  vertical-align: middle;
}

.modal-container {
  width: 600px;
  margin: 0 auto;
  padding: 20px 30px;
  background-color: #fff;
  border-radius: 2px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.33);
  transition: all 0.3s ease;
}
</style>
