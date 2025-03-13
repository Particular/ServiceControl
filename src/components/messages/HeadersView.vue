<script setup lang="ts">
import { ExtendedFailedMessage } from "@/resources/FailedMessage";

const props = defineProps<{
  message: ExtendedFailedMessage;
}>();
</script>

<template>
  <table class="table" v-if="!props.message.headersNotFound">
    <tbody>
      <tr class="interactiveList" v-for="(header, index) in props.message.headers" :key="index">
        <td nowrap="nowrap">{{ header.key }}</td>
        <td>
          <pre>{{ header.value }}</pre>
        </td>
      </tr>
    </tbody>
  </table>
  <div v-if="props.message.headersNotFound" class="alert alert-info">Could not find message headers. This could be because the message URL is invalid or the corresponding message was processed and is no longer tracked by ServiceControl.</div>
</template>

<style scoped></style>
