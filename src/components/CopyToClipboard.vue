<script setup lang="ts">
import { Tippy, TippyComponent } from "vue-tippy";
import { ref, useTemplateRef, watch } from "vue";
import FAIcon from "@/components/FAIcon.vue";
import { faCopy } from "@fortawesome/free-regular-svg-icons";

const props = withDefaults(
  defineProps<{
    value: string;
    isIconOnly?: boolean;
  }>(),
  { isIconOnly: false }
);

const tippyRef = useTemplateRef<TippyComponent | null>("tippyRef");
const timeoutId = ref(0);

async function copyToClipboard() {
  await navigator.clipboard.writeText(props.value);

  tippyRef.value?.show();
  timeoutId.value = window.setTimeout(() => tippyRef.value?.hide(), 3000);
}

watch(timeoutId, (_, previousTimeoutId) => window.clearTimeout(previousTimeoutId));
</script>

<template>
  <Tippy content="Copied" ref="tippyRef" trigger="manual">
    <button v-if="!props.isIconOnly" type="button" class="btn btn-secondary btn-sm" @click="copyToClipboard"><FAIcon :icon="faCopy" /> Copy to clipboard</button>
    <button v-else type="button" class="btn btn-secondary btn-sm" @click="copyToClipboard" v-tippy="'Copy to clipboard'"><FAIcon :icon="faCopy" /></button>
  </Tippy>
</template>
