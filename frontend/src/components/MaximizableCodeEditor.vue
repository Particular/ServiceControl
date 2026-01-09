<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";
import CodeEditor from "@/components/CodeEditor.vue";
import DiffMaximizeIcon from "@/assets/diff-maximize.svg";
import DiffCloseIcon from "@/assets/diff-close.svg";
import { Extension } from "@codemirror/state";
import { CodeLanguage } from "@/components/codeEditorTypes";

const modelValue = defineModel<string>({ required: true });

withDefaults(
  defineProps<{
    language?: CodeLanguage;
    readOnly?: boolean;
    showGutter?: boolean;
    ariaLabel?: string;
    extensions?: Extension[];
    modalTitle?: string;
  }>(),
  {
    readOnly: false,
    showGutter: false,
    extensions: () => [],
    modalTitle: "Code View",
  }
);

// Component state for maximize functionality
const showMaximizeModal = ref(false);
const showMaximizeButton = ref(false);

// Handle maximize functionality
const toggleMaximizeModal = () => {
  showMaximizeModal.value = !showMaximizeModal.value;
};

// Handle mouse enter/leave for showing maximize button
const onEditorMouseEnter = () => {
  showMaximizeButton.value = true;
};

const onEditorMouseLeave = () => {
  showMaximizeButton.value = false;
};

// Handle ESC key to close modal
const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === "Escape" && showMaximizeModal.value) {
    showMaximizeModal.value = false;
  }
};

// Setup keyboard events for maximize modal
onMounted(() => {
  window.addEventListener("keydown", handleKeyDown);
});

// Clean up event listeners when component is destroyed
onBeforeUnmount(() => {
  window.removeEventListener("keydown", handleKeyDown);
});
</script>

<template>
  <div class="code-editor-wrapper" @mouseenter="onEditorMouseEnter" @mouseleave="onEditorMouseLeave">
    <!-- Regular CodeEditor -->
    <div class="editor-container">
      <CodeEditor class="maximazable-code-editor--inline-instance" v-model="modelValue" :language="language" :read-only="readOnly" :show-gutter="showGutter" :show-copy-to-clipboard="false" :aria-label="ariaLabel" :extensions="extensions">
        <template #toolbarLeft>
          <slot name="toolbarLeft"></slot>
        </template>
        <template #toolbarRight>
          <slot name="toolbarRight">
            <!-- Maximize Button (shown on hover) -->
            <button v-if="showMaximizeButton" @click="toggleMaximizeModal" class="maximize-button" v-tippy="`Maximize view`">
              <img :src="DiffMaximizeIcon" alt="Maximize" width="14" height="14" />
            </button>
          </slot>
        </template>
      </CodeEditor>
    </div>

    <!-- Maximize modal for CodeEditor -->
    <div v-if="showMaximizeModal" class="maximize-modal">
      <div class="maximize-modal-content">
        <div class="maximize-modal-toolbar">
          <span class="maximize-modal-title">{{ modalTitle }}</span>
          <button @click="toggleMaximizeModal" class="maximize-modal-close" v-tippy="`Close`">
            <img :src="DiffCloseIcon" alt="Close" width="16" height="16" />
          </button>
        </div>
        <div class="maximize-modal-body">
          <CodeEditor class="maximazable-code-editor--pop-up-instance" v-model="modelValue" :language="language" :read-only="readOnly" :show-copy-to-clipboard="true" :show-gutter="true" :aria-label="ariaLabel" :extensions="[]" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.code-editor-wrapper {
  position: relative;
  width: 100%;
}

.maximize-button {
  position: absolute;
  right: 0.375rem;
  top: 0.375rem;
  z-index: 10;
  background-color: rgba(255, 255, 255, 0.7);
  border: 1px solid #ddd;
  border-radius: 3px;
  padding: 0.25rem;
  cursor: pointer;
  opacity: 0.6;
  transition: opacity 0.2s ease;
}

.maximize-button:hover {
  opacity: 1;
}

.maximize-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 1000;
  display: flex;
  justify-content: center;
  align-items: center;
}

.maximize-modal-content {
  background-color: white;
  width: 95vw;
  height: 90vh;
  border-radius: 4px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.maximize-modal-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.625rem 0.9375rem;
  background-color: #f8f8f8;
  border-bottom: 1px solid #ddd;
}

.maximize-modal-title {
  font-weight: bold;
  font-size: 1rem;
}

.maximize-modal-close {
  background: none;
  border: none;
  cursor: pointer;
  padding: 0.3125rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.maximize-modal-body {
  flex: 1;
  overflow: auto;
  padding: 0;
}

/* Ensure the CodeEditor wrapper fills the modal body */
.maximize-modal-body :deep(.wrapper) {
  height: 100%;
  border-radius: 0;
}

.maximize-modal-body :deep(.cm-editor),
.maximize-modal-body :deep(.cm-scroller) {
  height: 100%;
}
</style>
