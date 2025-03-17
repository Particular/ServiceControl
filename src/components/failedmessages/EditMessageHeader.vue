<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";

interface MessageHeader {
  key: string;
  value?: string;
  isChanged: boolean;
  isMarkedAsRemoved: boolean;
  isLocked: boolean;
  isSensitive: boolean;
}

const settings = defineProps<{
  header: MessageHeader;
}>();

let origHeaderValue: string | undefined;
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
      <i class="fa fa-lock" v-tippy="`Protected system header`"></i>
    </span>
    <span v-if="(header.isChanged || header.isMarkedAsRemoved) && header.isSensitive">
      &nbsp;
      <i class="fa fa-exclamation-triangle" v-tippy="`This is a sensitive message header that if changed can the system behavior. Proceed with caution.`"></i>
    </span>
    <span v-if="header.isChanged">
      &nbsp;
      <i class="fa fa-pencil" v-tippy="`Edited`"></i>
    </span>
  </td>
  <td>
    <input :class="{ 'header-removed': header.isMarkedAsRemoved }" class="form-control" :disabled="header.isLocked" v-model="header.value" />
  </td>
  <td>
    <a v-if="!header.isLocked && !header.isMarkedAsRemoved" @click="markHeaderAsRemoved()">
      <i class="fa fa-trash" v-tippy="`Remove header`"></i>
    </a>
    <a v-if="header.isChanged" @click="resetHeaderChanges()">
      <i class="fa fa-undo" v-tippy="`Reset changes`"></i>
    </a>
  </td>
</template>

<style scoped>
span.header-removed {
  text-decoration: line-through 2px solid #ce4844;
}

input.header-removed {
  opacity: 0.3;
  pointer-events: none;
}

td[nowrap="nowrap"] {
  font-weight: bold;
}

td:first-child {
  padding-top: 15px;
  padding-left: 0;
  width: 30%;
}

td:first-child i.fa {
  font-size: 18px;
  padding-left: 6px;
  position: relative;
  top: 1px;
}

td:first-child i.fa.fa-exclamation-triangle {
  color: #ff9000;
}

td:first-child i.fa.fa-pencil {
  color: #8543e9 !important;
}

td:nth-child(3) {
  width: 60px;
  padding: 12px 0 0 10px;
}

td:nth-child(3) a {
  font-size: 18px;
}

td:nth-child(3) a:hover {
  cursor: pointer;
}

td:nth-child(3) i.fa.fa-trash {
  color: #555;
  margin-right: 10px;
}

td:nth-child(3) i.fa.fa-undo {
  color: #00a3c4;
}

td:nth-child(3) i.fa:hover {
  opacity: 0.8;
}
</style>
