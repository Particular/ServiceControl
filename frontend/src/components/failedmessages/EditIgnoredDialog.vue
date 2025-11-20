<script setup lang="ts">
const emit = defineEmits<{
  close: [];
}>();

function closeDialog() {
  emit("close");
}
</script>

<template>
  <section name="edit_ignored_dialog">
    <div class="modal" style="z-index: 1050; display: block" role="dialog" aria-label="edit ignored">
      <div class="modal-mask">
        <div class="modal-dialog">
          <div class="modal-content">
            <div class="modal-header">
              <div class="modal-title">
                <h3>Edit Ignored</h3>
              </div>
            </div>
            <div class="modal-body">
              <p>This retry was ignored because another edit had already been processed.</p>
            </div>
            <div class="modal-footer">
              <button class="btn btn-primary" @click="closeDialog()">Ok</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
@import "@/components/modal.css";
</style>
