<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import FAIcon from "@/components/FAIcon.vue";
import { faExclamationTriangle, faLock, faPencil, faTrash, faUndo } from "@fortawesome/free-solid-svg-icons";

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
    <FAIcon v-if="header.isLocked" :icon="faLock" class="icon" v-tippy="`Protected system header`" />
    <FAIcon
      v-if="(header.isChanged || header.isMarkedAsRemoved) && header.isSensitive"
      :icon="faExclamationTriangle"
      class="icon warning"
      size="lg"
      v-tippy="`This is a sensitive message header that if changed can the system behavior. Proceed with caution.`"
    />
    <FAIcon v-if="header.isChanged" :icon="faPencil" class="icon edit" v-tippy="`Edited`" />
  </td>
  <td>
    <input :class="{ 'header-removed': header.isMarkedAsRemoved }" class="form-control" :disabled="header.isLocked" v-model="header.value" />
  </td>
  <td>
    <div class="actions">
      <a v-if="!header.isLocked && !header.isMarkedAsRemoved" @click="markHeaderAsRemoved()">
        <FAIcon :icon="faTrash" class="remove" v-tippy="`Remove header`" />
      </a>
      <a v-if="header.isChanged" @click="resetHeaderChanges()">
        <FAIcon :icon="faUndo" class="undo" v-tippy="`Reset changes`" />
      </a>
    </div>
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

td:first-child .warning {
  color: #ff9000;
}

td:first-child .edit {
  color: #8543e9;
  font-size: 1.1em;
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

td:nth-child(3) .remove {
  color: #555;
}

td:nth-child(3) .undo {
  color: var(--sp-blue);
}

td:nth-child(3) i.fa:hover {
  opacity: 0.8;
}

.actions {
  display: flex;
  gap: 10px;
}

.icon {
  color: var(--reduced-emphasis);
  margin-left: 6px;
}
</style>
