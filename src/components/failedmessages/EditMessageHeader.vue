<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";

interface MessageHeader {
  key: string;
  value: string;
  isChanged: boolean;
  isMarkedAsRemoved: boolean;
  isLocked: boolean;
  isSensitive: boolean;
}

const settings = defineProps<{
  header: MessageHeader;
}>();

let origHeaderValue: string;
const header = ref<MessageHeader>(settings.header);

const headerValue = computed(() => settings.header.value);
watch(headerValue, (newValue) => {
  header.value.isChanged = newValue !== origHeaderValue;
});

function resetHeaderChanges() {
  header.value.value = origHeaderValue;
  header.value.isMarkedAsRemoved = false;
  header.value.isChanged = false;
}

function markHeaderAsRemoved() {
  header.value.isMarkedAsRemoved = true;
  header.value.isChanged = true;
}

onMounted(() => {
  origHeaderValue = settings.header.value;
});
</script>

<template>
  <td nowrap="nowrap">
    <span :class="{ 'header-removed': header.isMarkedAsRemoved }">{{ settings.header.key }}</span>
    <span v-if="header.isLocked">
      &nbsp;
      <i class="fa fa-lock" tooltip="Protected system header"></i>
    </span>
    <span v-if="(header.isChanged || header.isMarkedAsRemoved) && header.isSensitive">
      &nbsp;
      <i class="fa fa-exclamation-triangle" uib-tooltip="This is a sensitive message header that if changed can the system behavior. Proceed with caution."></i>
    </span>
    <span v-if="header.isChanged">
      &nbsp;
      <i class="fa fa-pencil" tooltip="Edited"></i>
    </span>
  </td>
  <td>
    <input :class="{ 'header-removed': header.isMarkedAsRemoved }" class="form-control" :disabled="header.isLocked" v-model="header.value" />
  </td>
  <td>
    <a v-if="!header.isLocked && !header.isMarkedAsRemoved" @click="markHeaderAsRemoved()">
      <i class="fa fa-trash" tooltip="Protected system header"></i>
    </a>
    <a v-if="header.isChanged" @click="resetHeaderChanges()">
      <i class="fa fa-undo" tooltip="Protected system header"></i>
    </a>
  </td>
</template>

<style>
span.header-removed {
  text-decoration: line-through 2px solid #ce4844;
}

input.header-removed {
  opacity: 0.3;
  pointer-events: none;
}
</style>
