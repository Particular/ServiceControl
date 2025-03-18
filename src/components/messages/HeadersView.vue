<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";
import CopyToClipboard from "@/components/CopyToClipboard.vue";
import { ref } from "vue";
const props = defineProps<{
  message: ExtendedFailedMessage;
}>();

const hoverStates = ref<Record<number, boolean>>({});

const toggleHover = (index: number, state: boolean) => {
  hoverStates.value[index] = state;
};
</script>

<template>
  <table class="table" v-if="!props.message.headersNotFound">
    <tbody>
      <tr class="interactiveList" v-for="(header, index) in props.message.headers" :key="index">
        <td nowrap="nowrap">{{ header.key }}</td>
        <td>
          <div class="headercopy" @mouseover="toggleHover(index, true)" @mouseleave="toggleHover(index, false)">
            <pre>{{ header.value }}</pre>
            <CopyToClipboard v-if="hoverStates[index]" :value="header.value || ''" :isIconOnly="true" />
          </div>
        </td>
      </tr>
    </tbody>
  </table>
  <div v-if="props.message.headersNotFound" class="alert alert-info">Could not find message headers. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
</template>

<style scoped>
.headercopy {
  display: flex;
  align-items: top;
  gap: 0.4rem;
}
</style>
