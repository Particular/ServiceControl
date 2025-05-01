<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";
import DiffContent from "./DiffContent.vue";
import DiffMaximizeIcon from "@/assets/diff-maximize.svg";
import DiffCloseIcon from "@/assets/diff-close.svg";

interface Props {
  oldValue: string;
  newValue: string;
  splitView?: boolean;
  hideLineNumbers?: boolean;
  showDiffOnly?: boolean;
  extraLinesSurroundingDiff?: number;
  leftTitle?: string;
  rightTitle?: string;
  compareMethod?: "diffChars" | "diffWords" | "diffWordsWithSpace" | "diffLines" | "diffTrimmedLines" | "diffSentences" | "diffCss";
  showMaximizeIcon?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  splitView: true,
  hideLineNumbers: false,
  showDiffOnly: false,
  extraLinesSurroundingDiff: 3,
  leftTitle: "Previous",
  rightTitle: "Current",
  compareMethod: "diffLines",
  showMaximizeIcon: false,
});

// Component state for maximize functionality
const showMaximizeModal = ref(false);
const showMaximizeButton = ref(false);

// Handle maximize functionality
const toggleMaximizeModal = () => {
  showMaximizeModal.value = !showMaximizeModal.value;
};

// Handle ESC key to close modal
const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === "Escape" && showMaximizeModal.value) {
    showMaximizeModal.value = false;
  }
};

// Handle mouse enter/leave for showing maximize button
const onDiffMouseEnter = () => {
  if (props.showMaximizeIcon) {
    showMaximizeButton.value = true;
  }
};

const onDiffMouseLeave = () => {
  showMaximizeButton.value = false;
};

// Setup keyboard events
onMounted(() => {
  if (props.showMaximizeIcon) {
    window.addEventListener("keydown", handleKeyDown);
  }
});

// Clean up event listeners when component is destroyed
onBeforeUnmount(() => {
  window.removeEventListener("keydown", handleKeyDown);
});
</script>

<template>
  <div class="diff-viewer" @mouseenter="onDiffMouseEnter" @mouseleave="onDiffMouseLeave">
    <div class="diff-wrapper">
      <!-- Maximize Button -->
      <button v-if="showMaximizeIcon && showMaximizeButton" @click="toggleMaximizeModal" class="maximize-button" title="Maximize diff view">
        <img :src="DiffMaximizeIcon" alt="Maximize" width="14" height="14" />
      </button>

      <!-- Regular DiffContent -->
      <DiffContent
        :old-value="oldValue"
        :new-value="newValue"
        :split-view="splitView"
        :hide-line-numbers="hideLineNumbers"
        :show-diff-only="showDiffOnly"
        :extra-lines-surrounding-diff="extraLinesSurroundingDiff"
        :left-title="leftTitle"
        :right-title="rightTitle"
        :compare-method="compareMethod"
      />
    </div>

    <!-- Maximize Modal with separate DiffContent instance -->
    <div v-if="showMaximizeModal" class="maximize-modal">
      <div class="maximize-modal-content">
        <div class="maximize-modal-toolbar">
          <span class="maximize-modal-title">{{ leftTitle }} vs {{ rightTitle }}</span>
          <button @click="toggleMaximizeModal" class="maximize-modal-close" title="Close">
            <img :src="DiffCloseIcon" alt="Close" width="16" height="16" />
          </button>
        </div>
        <div class="maximize-modal-body">
          <!-- Separate DiffContent instance for modal -->
          <DiffContent
            :old-value="oldValue"
            :new-value="newValue"
            :split-view="splitView"
            :hide-line-numbers="hideLineNumbers"
            :show-diff-only="showDiffOnly"
            :extra-lines-surrounding-diff="extraLinesSurroundingDiff"
            :left-title="leftTitle"
            :right-title="rightTitle"
            :compare-method="compareMethod"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.diff-viewer {
  width: 100%;
  overflow: hidden;
  font-family: monospace;
  font-size: 0.75rem;
  position: relative;
}

.diff-wrapper {
  width: 100%;
  position: relative;
}

/* Maximize button styles */
.maximize-button {
  position: absolute;
  right: 6px;
  z-index: 10;
  background-color: rgba(255, 255, 255, 0.7);
  border: 1px solid #ddd;
  border-radius: 3px;
  padding: 4px;
  cursor: pointer;
  opacity: 0.6;
  transition: opacity 0.2s ease;
}

.maximize-button:hover {
  opacity: 1;
}

/* Modal styles */
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
  width: calc(100% - 40px);
  height: calc(100% - 40px);
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
  padding: 10px 15px;
  background-color: #f8f8f8;
  border-bottom: 1px solid #ddd;
}

.maximize-modal-title {
  font-weight: bold;
  font-size: 16px;
}

.maximize-modal-close {
  background: none;
  border: none;
  cursor: pointer;
  padding: 5px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #666;
}

.maximize-modal-close:hover {
  color: #000;
}

.maximize-modal-body {
  flex: 1;
  overflow: auto;
  padding: 0;
}

.maximize-modal-body :deep(.diff-container) {
  height: 100%;
}

.maximize-modal-body :deep(.diff-content) {
  max-height: calc(100% - 35px);
  overflow: auto;
}
</style>
