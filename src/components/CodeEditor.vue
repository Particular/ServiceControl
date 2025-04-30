<script setup lang="ts">
import CodeMirror from "vue-codemirror6";
import { json } from "@codemirror/lang-json";
import { xml } from "@codemirror/lang-xml";
import { StreamLanguage } from "@codemirror/language";
import { powerShell } from "@codemirror/legacy-modes/mode/powershell";
import { shell } from "@codemirror/legacy-modes/mode/shell";
import { csharp } from "@codemirror/legacy-modes/mode/clike";
import { Extension } from "@codemirror/state";
import { CodeLanguage } from "@/components/codeEditorTypes";
import CopyToClipboard from "@/components/CopyToClipboard.vue";
import { computed } from "vue";

const code = defineModel<string>({ required: true });
const props = withDefaults(
  defineProps<{
    language?: CodeLanguage;
    readOnly?: boolean;
    showGutter?: boolean;
    showCopyToClipboard?: boolean;
    ariaLabel?: string;
    css?: string;
  }>(),
  { readOnly: true, showGutter: true, showCopyToClipboard: true }
);

const extensions = computed(() => {
  const extensions: Extension[] = [];

  switch (props.language) {
    case "json":
      extensions.push(json());
      break;
    case "xml":
      extensions.push(xml());
      break;
    case "shell":
      extensions.push(StreamLanguage.define(shell));
      break;
    case "powershell":
      extensions.push(StreamLanguage.define(powerShell));
      break;
    case "csharp":
      extensions.push(StreamLanguage.define(csharp));
      break;
  }

  return extensions;
});
</script>

<template>
  <div class="wrapper" :aria-label="ariaLabel" :class="css">
    <div v-if="props.showCopyToClipboard || $slots.toolbarLeft || $slots.toolbarRight" class="toolbar">
      <div><slot name="toolbarLeft"></slot></div>
      <div>
        <slot name="toolbarRight"></slot>
        <CopyToClipboard class="clipboard" v-if="props.showCopyToClipboard" :value="code" />
      </div>
    </div>
    <CodeMirror v-model="code" :extensions="extensions" :basic="props.showGutter" :minimal="!props.showGutter" :readonly="props.readOnly" :gutter="!props.readOnly" :wrap="true"></CodeMirror>
  </div>
</template>

<style scoped>
.wrapper {
  margin-top: 5px;
  border-radius: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ccc;
  background: white;
  display: flex;
  flex-direction: column;
}
.toolbar {
  background-color: #f3f3f3;
  border: #8c8c8c 1px solid;
  border-radius: 3px;
  padding: 5px;
  margin-bottom: 0.5rem;
  display: flex;
  flex-direction: row;
  justify-content: space-between;
  align-items: center;
}
.clipboard {
  margin-left: 0.5rem;
}
</style>
