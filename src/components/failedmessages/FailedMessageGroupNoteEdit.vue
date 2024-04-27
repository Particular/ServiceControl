<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";

const emit = defineEmits(["createNoteConfirmed", "editNoteConfirmed", "cancelEditNote"]);

interface GroupComment {
  groupid: string;
  comment?: string;
}

const settings = defineProps<GroupComment>();
const grpcomment = ref(settings.comment);

function createNote() {
  const updatedGroup = {
    groupid: settings.groupid,
    comment: grpcomment.value,
  };
  emit("createNoteConfirmed", updatedGroup);
}

function editNote() {
  const updatedGroup = {
    groupid: settings.groupid,
    comment: grpcomment.value,
  };
  emit("editNoteConfirmed", updatedGroup);
}

function close() {
  emit("cancelEditNote");
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
          <h3 class="modal-title" v-if="settings.comment">Modify Note</h3>
          <h3 class="modal-title" v-if="!settings.comment">Create Note</h3>
        </div>

        <form name="commentNoteForm" novalidate>
          <div class="modal-body">
            <div class="row">
              <div class="form-group">
                <label for="comment">Note</label>
                <textarea type="text" id="txtcomment" name="txtcomment" v-model.trim="grpcomment" placeholder="Comment" :minInputLength="1" class="form-control" required></textarea>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button v-if="settings.comment" :disabled="!grpcomment" class="btn btn-primary" @click="editNote">Modify</button>
            <button v-if="!settings.comment" :disabled="!grpcomment" class="btn btn-primary" @click="createNote">Create</button>
            <button class="btn btn-default" @click="close">Cancel</button>
          </div>
        </form>
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
  margin: 0px auto;
  padding: 20px 30px;
  background-color: #fff;
  border-radius: 2px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.33);
  transition: all 0.3s ease;
}
</style>
