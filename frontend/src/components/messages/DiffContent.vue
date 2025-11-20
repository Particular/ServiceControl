<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { diffLines, diffWords, diffWordsWithSpace, diffChars, diffTrimmedLines, diffSentences, diffCss } from "diff";

// Types needed for the diff viewer
interface DiffChange {
  value: string;
  added?: boolean;
  removed?: boolean;
}

interface LineInformation {
  left: DiffInformation;
  right: DiffInformation;
}

interface DiffInformation {
  value: string;
  lineNumber?: number;
  type: DiffType;
}

enum DiffType {
  DEFAULT = 0,
  ADDED = 1,
  REMOVED = 2,
}

// Types for rendered diff items
interface FoldItem {
  type: "fold";
  count: number;
  blockNumber: number;
  leftLineNumber?: number;
  rightLineNumber?: number;
}

interface LineItem {
  type: "line";
  lineInfo: LineInformation;
  index: number;
}

type DiffItem = FoldItem | LineItem;

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
}

const props = withDefaults(defineProps<Props>(), {
  splitView: true,
  hideLineNumbers: false,
  showDiffOnly: false,
  extraLinesSurroundingDiff: 3,
  leftTitle: "Previous",
  rightTitle: "Current",
  compareMethod: "diffLines",
});

// Component state
const lineInformation = ref<LineInformation[]>([]);
const diffLineIndexes = ref<number[]>([]);
const expandedBlocks = ref<number[]>([]);

// Compute diff when inputs change
const computeDiff = (): void => {
  const { oldValue, newValue, compareMethod } = props;

  // Skip processing if values are identical
  if (oldValue === newValue) {
    const lines = newValue.split("\n");
    if (lines[lines.length - 1] === "") {
      lines.pop();
    }

    const result: LineInformation[] = [];
    let lineNumber = 1;

    lines.forEach((line) => {
      result.push({
        left: { value: line, lineNumber: lineNumber, type: DiffType.DEFAULT },
        right: { value: line, lineNumber: lineNumber++, type: DiffType.DEFAULT },
      });
    });

    lineInformation.value = result;
    diffLineIndexes.value = [];
    return;
  }

  // Generate diff based on selected method
  let diffOutput: DiffChange[];
  switch (compareMethod) {
    case "diffChars":
      diffOutput = diffChars(oldValue, newValue);
      break;
    case "diffWords":
      diffOutput = diffWords(oldValue, newValue);
      break;
    case "diffWordsWithSpace":
      diffOutput = diffWordsWithSpace(oldValue, newValue);
      break;
    case "diffTrimmedLines":
      diffOutput = diffTrimmedLines(oldValue, newValue);
      break;
    case "diffSentences":
      diffOutput = diffSentences(oldValue, newValue);
      break;
    case "diffCss":
      diffOutput = diffCss(oldValue, newValue);
      break;
    case "diffLines":
    default:
      diffOutput = diffLines(oldValue, newValue);
      break;
  }

  // Process the diff output into line information
  const result: LineInformation[] = [];
  const diffLinesArray: number[] = [];
  let leftLineNumber = 1;
  let rightLineNumber = 1;
  let counter = 0;

  diffOutput.forEach((part) => {
    const lines = part.value.split("\n");
    // Remove empty line at the end if it exists
    if (lines[lines.length - 1] === "") {
      lines.pop();
    }

    if (!part.added && !part.removed) {
      // Unchanged lines
      lines.forEach((line) => {
        result.push({
          left: { value: line, lineNumber: leftLineNumber++, type: DiffType.DEFAULT },
          right: { value: line, lineNumber: rightLineNumber++, type: DiffType.DEFAULT },
        });
        counter++;
      });
    } else if (part.removed) {
      // Removed lines (left side)
      lines.forEach((line) => {
        diffLinesArray.push(counter);
        result.push({
          left: { value: line, lineNumber: leftLineNumber++, type: DiffType.REMOVED },
          right: { value: "", type: DiffType.DEFAULT },
        });
        counter++;
      });
    } else if (part.added) {
      // Added lines (right side)
      lines.forEach((line) => {
        diffLinesArray.push(counter);
        result.push({
          left: { value: "", type: DiffType.DEFAULT },
          right: { value: line, lineNumber: rightLineNumber++, type: DiffType.ADDED },
        });
        counter++;
      });
    }
  });

  lineInformation.value = result;
  diffLineIndexes.value = diffLinesArray;

  // Reset expanded blocks when diff changes
  expandedBlocks.value = [];
};

// Toggle a code fold block
const onBlockExpand = (id: number): void => {
  if (!expandedBlocks.value.includes(id)) {
    expandedBlocks.value.push(id);
  }
};

// Render the diff with code folding for unchanged lines
const renderDiff = computed<DiffItem[]>(() => {
  if (!lineInformation.value.length) return [];

  const { showDiffOnly, extraLinesSurroundingDiff } = props;
  const extraLines = extraLinesSurroundingDiff < 0 ? 0 : extraLinesSurroundingDiff;

  let skippedLines: number[] = [];
  const result: DiffItem[] = [];

  // Create a mutable copy of diffLines for manipulation in the loop
  const currentDiffLines = [...diffLineIndexes.value];

  lineInformation.value.forEach((line, i) => {
    const diffBlockStart = currentDiffLines[0];
    const currentPosition = diffBlockStart !== undefined ? diffBlockStart - i : undefined;

    // Check if this line should be shown or folded
    if (showDiffOnly) {
      // At boundary of diff section, process any accumulated skipped lines
      if (currentPosition === -extraLines) {
        skippedLines = [];
        currentDiffLines.shift();
      }

      // If this is a default line far from changes and not in an expanded block, accumulate it
      if (line.left.type === DiffType.DEFAULT && ((currentPosition !== undefined && currentPosition > extraLines) || typeof diffBlockStart === "undefined") && !expandedBlocks.value.includes(diffBlockStart)) {
        skippedLines.push(i + 1);

        // If we're at the end and have accumulated skipped lines, render the fold indicator
        if (i === lineInformation.value.length - 1 && skippedLines.length > 1) {
          result.push({
            type: "fold",
            count: skippedLines.length,
            blockNumber: diffBlockStart,
            leftLineNumber: line.left.lineNumber,
            rightLineNumber: line.right.lineNumber,
          });
        }
        return;
      }
    }

    // If we have accumulated skipped lines and this line should be shown, display the fold indicator
    if (currentPosition === extraLines && skippedLines.length > 0) {
      const count = skippedLines.length;
      skippedLines = [];

      result.push({
        type: "fold",
        count,
        blockNumber: diffBlockStart,
        leftLineNumber: line.left.lineNumber,
        rightLineNumber: line.right.lineNumber,
      });
    }

    // Add the actual line content
    result.push({
      type: "line",
      lineInfo: line,
      index: i,
    });
  });

  return result;
});

// Compute the diff on initial load and when inputs change
watch(
  () => [props.oldValue, props.newValue, props.compareMethod, props.showDiffOnly, props.extraLinesSurroundingDiff],
  () => {
    computeDiff();
  },
  { immediate: true }
);
</script>

<template>
  <div class="diff-container" :class="{ 'split-view': splitView }">
    <!-- Headers -->
    <div v-if="leftTitle || rightTitle" class="diff-headers">
      <div class="diff-header">{{ leftTitle }}</div>
      <div v-if="splitView" class="diff-header">{{ rightTitle }}</div>
    </div>

    <!-- Diff content -->
    <div class="diff-content">
      <!-- Left side (old) -->
      <div v-if="splitView" class="diff-column">
        <div class="diff-lines">
          <template v-for="(item, itemIndex) in renderDiff" :key="`diff-left-${itemIndex}`">
            <!-- Code fold indicator -->
            <div v-if="item.type === 'fold'" class="diff-fold">
              <button @click="onBlockExpand(item.blockNumber)" class="diff-fold-button">
                {{ `⟨ Expand ${item.count} lines... ⟩` }}
              </button>
            </div>

            <!-- Regular line content -->
            <div v-else-if="item.type === 'line'" :class="['diff-line', { 'diff-line-removed': item.lineInfo.left.type === DiffType.REMOVED }]">
              <span v-if="!hideLineNumbers" class="diff-line-number">{{ item.lineInfo.left.lineNumber }}</span>
              <span class="diff-line-content">{{ item.lineInfo.left.value }}</span>
            </div>
          </template>
        </div>
      </div>

      <!-- Right side (new) -->
      <div class="diff-column">
        <div class="diff-lines">
          <template v-for="(item, itemIndex) in renderDiff" :key="`diff-right-${itemIndex}`">
            <!-- Code fold indicator -->
            <div v-if="item.type === 'fold'" class="diff-fold">
              <button @click="onBlockExpand(item.blockNumber)" class="diff-fold-button">
                {{ `⟨ Expand ${item.count} lines... ⟩` }}
              </button>
            </div>

            <!-- Regular line content -->
            <div v-else-if="item.type === 'line'" :class="['diff-line', { 'diff-line-added': item.lineInfo.right.type === DiffType.ADDED }]">
              <span v-if="!hideLineNumbers" class="diff-line-number">{{ item.lineInfo.right.lineNumber }}</span>
              <span class="diff-line-content">{{ item.lineInfo.right.value }}</span>
            </div>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.diff-container {
  width: 100%;
}

.diff-headers {
  display: flex;
  border-bottom: 1px solid #ddd;
  background-color: #f1f1f1;
}

.diff-header {
  flex: 1;
  padding: 6px 10px;
  font-weight: bold;
  font-size: 0.8rem;
  text-align: center;
}

.diff-content {
  display: flex;
}

.diff-column {
  flex: 1;
  overflow: visible;
}

.split-view .diff-column:first-child {
  border-right: 1px solid #ddd;
}

.diff-lines {
  padding: 5px 0;
}

.diff-line {
  padding: 0 5px;
  white-space: pre-wrap;
  word-break: break-all;
  display: flex;
}

.diff-line-number {
  min-width: 40px;
  color: #999;
  text-align: right;
  padding-right: 10px;
  user-select: none;
}

.diff-line-content {
  flex: 1;
}

.diff-line-added {
  background-color: #e6ffed;
  color: #28a745;
}

.diff-line-removed {
  background-color: #ffeef0;
  color: #d73a49;
}

/* Code fold styling */
.diff-fold {
  text-align: center;
  padding: 2px 0;
}

.diff-fold-button {
  background: none;
  border: none;
  color: #0366d6;
  padding: 2px 8px;
  font-size: 0.7rem;
  cursor: pointer;
  font-family: monospace;
}

.diff-fold-button:hover {
  text-decoration: underline;
}
</style>
