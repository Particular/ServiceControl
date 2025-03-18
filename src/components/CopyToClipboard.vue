<script setup lang="ts">
import { Tippy, TippyComponent } from "vue-tippy";
import { useTemplateRef } from "vue";

const props = withDefaults(
  defineProps<{
    value: string;
    isIconOnly?: boolean;
  }>(),
  { isIconOnly: false }
);

const tippyRef = useTemplateRef<TippyComponent | null>("tippyRef");
let timeoutId: number;

async function copyToClipboard() {
  await navigator.clipboard.writeText(props.value);
  window.clearTimeout(timeoutId);
  tippyRef.value?.show();
  timeoutId = window.setTimeout(() => tippyRef.value?.hide(), 3000);
}
</script>

<template>
  <Tippy content="Copied" ref="tippyRef" trigger="manual">
    <button v-if="!props.isIconOnly" type="button" class="btn btn-secondary btn-sm" @click="copyToClipboard"><i class="fa fa-copy"></i> Copy to clipboard</button>
    <button v-else type="button" class="btn btn-secondary btn-sm" @click="copyToClipboard" v-tippy="'Copy to clipboard'"><i class="fa fa-copy"></i></button>
  </Tippy>
</template>
