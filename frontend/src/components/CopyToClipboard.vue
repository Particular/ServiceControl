<script setup lang="ts">
import { Tippy, TippyComponent } from "vue-tippy";
import { ref, useTemplateRef, watch } from "vue";
import ActionButton from "@/components/ActionButton.vue";
import { faCopy } from "@fortawesome/free-regular-svg-icons";

const props = withDefaults(
  defineProps<{
    value: string;
    isIconOnly?: boolean;
  }>(),
  { isIconOnly: false }
);

const tippyRef = useTemplateRef<TippyComponent | null>("tippyRef");
const timeoutId = ref<ReturnType<typeof setTimeout>>();

async function copyToClipboard() {
  await navigator.clipboard.writeText(props.value);

  tippyRef.value?.show();
  timeoutId.value = setTimeout(() => tippyRef.value?.hide(), 3000);
}

watch(timeoutId, (_, previousTimeoutId) => clearTimeout(previousTimeoutId));
</script>

<template>
  <Tippy content="Copied" ref="tippyRef" trigger="manual">
    <ActionButton v-if="!props.isIconOnly" variant="secondary" size="sm" :icon="faCopy" @click="copyToClipboard">Copy to clipboard</ActionButton>
    <ActionButton v-else variant="secondary" size="sm" :icon="faCopy" tooltip="Copy to clipboard" @click="copyToClipboard" />
  </Tippy>
</template>
